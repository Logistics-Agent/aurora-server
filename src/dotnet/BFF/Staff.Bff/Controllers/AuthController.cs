using System.Globalization;
using System.Security.Claims;
using Asp.Versioning;
using Auth.Grpc;
using BFF.RateLimiting;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Options;
using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Authentication endpoints (mọi persona đều đi qua đây — gateway catch-all /api/v1/** → Staff.Bff).
/// Token KHÔNG BAO GIỜ trả trong body — chỉ set HttpOnly cookies:
///   - .Aurora.Auth  (Session cookie DataProtection ASP.NET Core)
///   - access_token  (Path=/, MaxAge=expiresIn)
///   - refresh_token (Path=/api/v1/auth, MaxAge=30 ngày)
/// Rate limit chặt (auth-strict) chống brute-force.
/// </summary>
[ApiVersion("1.0")]
public class AuthController(
    AuthService.AuthServiceClient authClient,
    IOptions<AuthCookieOptions> cookieOptions,
    ILogger<AuthController> logger) : StaffControllerBase
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";
    private const string TenantCodeCookie = "tenant_code";
    private const string UserTypeCookie = "user_type";
    private const string RefreshCookiePath = "/api/v1/auth";
    private readonly AuthCookieOptions _cookieOpts = cookieOptions?.Value ?? new AuthCookieOptions();

    /// <summary>Kiểm tra email tồn tại + thuộc tenant nào (bước 1 của flow login).</summary>
    [HttpPost("identify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> Identify([FromBody] IdentifyBody body)
    {
        var response = await authClient.IdentifyUserAsync(
            new IdentifyUserRequest { Email = body.Email },
            GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));

        return Ok(new
        {
            response.Exists,
            response.TenantCode,
            response.UserType
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginBody body)
    {
        try
        {
            var identity = await authClient.IdentifyUserAsync(
                new IdentifyUserRequest { Email = body.Email },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));

            if (!string.IsNullOrWhiteSpace(body.TenantCode) &&
                !string.Equals(identity.TenantCode, body.TenantCode, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { detail = "Tenant code does not match the account." });
            }

            var response = await authClient.LoginAsync(
                new LoginRequest
                {
                    TenantCode = body.TenantCode ?? identity.TenantCode ?? string.Empty,
                    Email = body.Email,
                    Password = body.Password
                },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.LoginTimeout, HttpContext.RequestAborted));

            await SignInUserAsync(response, body.Email, body.TenantCode ?? identity.TenantCode ?? string.Empty, identity.UserType, identity.PermissionVersion);

            logger.LogInformation("User {Email} logged in (userId={UserId})", body.Email, response.UserId);

            // Token nằm trong HttpOnly cookie — body chỉ chứa thông tin phiên
            return Ok(new
            {
                response.UserId,
                response.TenantId,
                Roles = response.Roles.ToList(),
                Permissions = response.Permissions.ToList(),
                response.ExpiresIn
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
        {
            var detail = !string.IsNullOrWhiteSpace(ex.Status.Detail) ? ex.Status.Detail : "Invalid credentials.";
            return Unauthorized(new { detail });
        }
        catch (RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.PermissionDenied or Grpc.Core.StatusCode.FailedPrecondition)
        {
            // User mới invite còn ở trạng thái FORCE_CHANGE_PASSWORD
            return Conflict(new
            {
                detail = "Tài khoản cần hoàn tất lời mời (đặt mật khẩu mới).",
                requiresInvitationCompletion = true,
                session = ex.Status.Detail
            });
        }
    }

    /// <summary>Hoàn tất lời mời: đặt mật khẩu mới — login luôn nếu thành công.</summary>
    [HttpPost("complete-invitation")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> CompleteInvitation([FromBody] CompleteInvitationBody body)
    {
        try
        {
            var identity = await authClient.IdentifyUserAsync(
                new IdentifyUserRequest { Email = body.Email },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));

            var response = await authClient.CompleteInvitationAsync(
                new CompleteInvitationRequest
                {
                    Email = body.Email,
                    NewPassword = body.NewPassword,
                    ConfirmationCode = body.ConfirmationCode
                },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.LoginTimeout, HttpContext.RequestAborted));

            await SignInUserAsync(response, body.Email, identity.TenantCode, identity.UserType, identity.PermissionVersion);

            logger.LogInformation("User {Email} completed invitation", body.Email);

            return Ok(new
            {
                response.UserId,
                response.TenantId,
                Roles = response.Roles.ToList(),
                Permissions = response.Permissions.ToList(),
                response.ExpiresIn
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
        {
            return Unauthorized(new { detail = "Invalid confirmation code or session expired." });
        }
    }

    /// <summary>Refresh access token từ refresh_token cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { detail = "Missing refresh token." });
        }

        try
        {
            var response = await authClient.RefreshTokenAsync(
                new RefreshTokenRequest
                {
                    RefreshToken = refreshToken,
                    TenantCode = Request.Cookies[TenantCodeCookie] ?? string.Empty,
                    UserType = Request.Cookies[UserTypeCookie] ?? string.Empty
                },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.RefreshTimeout, HttpContext.RequestAborted));

            var tenantCode = Request.Cookies[TenantCodeCookie] ?? string.Empty;
            var userType = Request.Cookies[UserTypeCookie] ?? string.Empty;
            SetAuthCookies(response, tenantCode, userType);

            return Ok(new { response.ExpiresIn });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unimplemented)
        {
            // AuthGrpcService.RefreshToken chưa được implement phía IamTenant
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { detail = "Refresh token chưa được hỗ trợ — vui lòng đăng nhập lại." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ClearAuthCookies();
            return Unauthorized(new { detail = "Refresh token invalid or expired." });
        }
    }

    /// <summary>Đăng xuất: revoke best-effort phía server + xóa cookies.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await authClient.LogoutAsync(
                    new LogoutRequest { RefreshToken = refreshToken },
                    GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));
            }
            catch (RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.Unimplemented or Grpc.Core.StatusCode.Unavailable)
            {
                logger.LogWarning("Server-side logout unavailable: {Status}", ex.StatusCode);
            }
        }

        // Luôn xóa session ASP.NET Core + cookies phía client dù server revoke thất bại
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearAuthCookies();
        return NoContent();
    }

    // --- Sign-in & Cookie helpers ---

    private async Task SignInUserAsync(LoginResponse response, string email, string tenantCode, string userType, int? permissionVersion = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, response.UserId),
            new(JwtClaims.UserId, response.UserId),
            new(ClaimTypes.Email, email),
            new("email", email),
            new("username", email)
        };

        if (!string.IsNullOrWhiteSpace(response.TenantId))
        {
            claims.Add(new Claim(JwtClaims.TenantId, response.TenantId));
            claims.Add(new Claim("tenant_id", response.TenantId));
        }

        if (permissionVersion.HasValue && permissionVersion.Value > 0)
        {
            claims.Add(new Claim(JwtClaims.PermissionVersion, permissionVersion.Value.ToString()));
            claims.Add(new Claim("permission_version", permissionVersion.Value.ToString()));
        }

        if (email.Contains('@'))
        {
            claims.Add(new Claim("email_domain", email.Split('@')[1]));
        }

        foreach (var role in response.Roles)
        {
            var canonicalRole = role.Trim().ToUpperInvariant() switch
            {
                "SYSTEMADMIN" or "SYSTEM_ADMIN" => RoleConstants.SystemAdmin,
                "TENANTADMIN" or "TENANT_ADMIN" => RoleConstants.TenantAdmin,
                "MANAGER" => RoleConstants.Manager,
                _ => role
            };

            claims.Add(new Claim(ClaimTypes.Role, canonicalRole));
            claims.Add(new Claim("role", canonicalRole));
            claims.Add(new Claim("cognito:groups", canonicalRole));
            claims.Add(new Claim(JwtClaims.Role, canonicalRole));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn)
        };

        authProperties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = response.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = response.RefreshToken ?? string.Empty },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn).ToString("o", CultureInfo.InvariantCulture) }
        });

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        SetAuthCookies(response, tenantCode, userType);
    }

    private void SetAuthCookies(LoginResponse response, string tenantCode, string userType)
    {
        var sameSiteMode = _cookieOpts.SameSite?.Equals("None", StringComparison.OrdinalIgnoreCase) == true
            ? SameSiteMode.None
            : _cookieOpts.SameSite?.Equals("Strict", StringComparison.OrdinalIgnoreCase) == true
                ? SameSiteMode.Strict
                : SameSiteMode.Lax;

        var secure = sameSiteMode == SameSiteMode.None || _cookieOpts.Secure;

        var baseCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSiteMode
        };

        if (!string.IsNullOrWhiteSpace(_cookieOpts.Domain))
            baseCookieOptions.Domain = _cookieOpts.Domain;

        Response.Cookies.Append(AccessTokenCookie, response.AccessToken, new CookieOptions
        {
            HttpOnly = baseCookieOptions.HttpOnly,
            Secure = baseCookieOptions.Secure,
            SameSite = baseCookieOptions.SameSite,
            Domain = baseCookieOptions.Domain,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(response.ExpiresIn)
        });

        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            Response.Cookies.Append(RefreshTokenCookie, response.RefreshToken, new CookieOptions
            {
                HttpOnly = baseCookieOptions.HttpOnly,
                Secure = baseCookieOptions.Secure,
                SameSite = baseCookieOptions.SameSite,
                Domain = baseCookieOptions.Domain,
                Path = RefreshCookiePath, // chỉ gửi kèm cho /api/v1/auth/*
                MaxAge = TimeSpan.FromDays(30)
            });

            Response.Cookies.Append(TenantCodeCookie, tenantCode, new CookieOptions
            {
                HttpOnly = baseCookieOptions.HttpOnly,
                Secure = baseCookieOptions.Secure,
                SameSite = baseCookieOptions.SameSite,
                Domain = baseCookieOptions.Domain,
                Path = RefreshCookiePath,
                MaxAge = TimeSpan.FromDays(30)
            });

            Response.Cookies.Append(UserTypeCookie, userType, new CookieOptions
            {
                HttpOnly = baseCookieOptions.HttpOnly,
                Secure = baseCookieOptions.Secure,
                SameSite = baseCookieOptions.SameSite,
                Domain = baseCookieOptions.Domain,
                Path = RefreshCookiePath,
                MaxAge = TimeSpan.FromDays(30)
            });
        }
    }

    /// <summary>Yêu cầu gửi mã OTP đặt lại mật khẩu qua email.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordBody body)
    {
        try
        {
            await authClient.ForgotPasswordAsync(
                new ForgotPasswordRequest { Email = body.Email },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));

            logger.LogInformation("Password reset requested for {Email}", body.Email);
            return Ok(new { message = "Mã xác nhận đã được gửi đến email nếu tài khoản tồn tại." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            // Tránh user enumeration
            return Ok(new { message = "Mã xác nhận đã được gửi đến email nếu tài khoản tồn tại." });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "ForgotPassword failed for {Email}: {Detail}", body.Email, ex.Status.Detail);
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>Xác nhận mã OTP và đặt mật khẩu mới.</summary>
    [HttpPost("reset-password")]
    [HttpPost("confirm-forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordBody body)
    {
        try
        {
            await authClient.ConfirmForgotPasswordAsync(
                new ConfirmForgotPasswordRequest
                {
                    Email = body.Email,
                    NewPassword = body.NewPassword,
                    ConfirmationCode = body.ConfirmationCode
                },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));

            logger.LogInformation("Password reset successfully for {Email}", body.Email);
            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "ResetPassword failed for {Email}: {Detail}", body.Email, ex.Status.Detail);
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>Đổi mật khẩu cho người dùng đang đăng nhập.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AuthPolicy)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordBody body)
    {
        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new { detail = "Không tìm thấy thông tin email phiên đăng nhập." });
        }

        try
        {
            var tenantCode = Request.Cookies[TenantCodeCookie] ?? string.Empty;
            var userType = Request.Cookies[UserTypeCookie] ?? string.Empty;

            await authClient.ChangePasswordAsync(
                new ChangePasswordRequest
                {
                    Email = email,
                    CurrentPassword = body.CurrentPassword,
                    NewPassword = body.NewPassword,
                    TenantCode = tenantCode,
                    UserType = userType
                },
                GrpcDeadlines.WithDeadline(GrpcDeadlines.DefaultTimeout, HttpContext.RequestAborted));

            logger.LogInformation("Password changed successfully for {Email}", email);
            return Ok(new { message = "Đổi mật khẩu thành công." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
        {
            return Unauthorized(new { detail = "Mật khẩu hiện tại không chính xác." });
        }
        catch (RpcException ex)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AccessTokenCookie, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions { Path = RefreshCookiePath });
        Response.Cookies.Delete(TenantCodeCookie, new CookieOptions { Path = RefreshCookiePath });
        Response.Cookies.Delete(UserTypeCookie, new CookieOptions { Path = RefreshCookiePath });
    }

    // --- DTOs ---
    public record IdentifyBody(string Email);
    public record LoginBody(string Email, string Password, string? TenantCode);
    public record CompleteInvitationBody(string Email, string NewPassword, string ConfirmationCode);
    public record ForgotPasswordBody(string Email);
    public record ResetPasswordBody(string Email, string NewPassword, string ConfirmationCode);
    public record ChangePasswordBody(string CurrentPassword, string NewPassword);
}
