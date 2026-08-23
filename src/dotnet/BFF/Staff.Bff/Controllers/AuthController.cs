using Asp.Versioning;
using Auth.Grpc;
using BFF.RateLimiting;
using BuildingBlocks.BFF.Extensions;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace StaffBff.Controllers;

/// <summary>
/// Authentication endpoints (mọi persona đều đi qua đây — gateway catch-all /api/v1/** → Staff.Bff).
/// Token KHÔNG BAO GIỜ trả trong body — chỉ set HttpOnly cookies:
///   - access_token  (Path=/, MaxAge=expiresIn)
///   - refresh_token (Path=/api/v1/auth, MaxAge=30 ngày)
/// Rate limit chặt (auth-strict) chống brute-force.
/// </summary>
[ApiVersion("1.0")]
public class AuthController(
    AuthService.AuthServiceClient authClient,
    ILogger<AuthController> logger) : StaffControllerBase
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";
    private const string TenantCodeCookie = "tenant_code";
    private const string UserTypeCookie = "user_type";
    private const string RefreshCookiePath = "/api/v1/auth";

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

            SetAuthCookies(response, body.TenantCode ?? identity.TenantCode ?? string.Empty, identity.UserType);

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
            return Unauthorized(new { detail = "Invalid credentials." });
        }
        catch (RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.PermissionDenied or Grpc.Core.StatusCode.FailedPrecondition)
        {
            // User mới invite còn ở trạng thái FORCE_CHANGE_PASSWORD
            return Conflict(new
            {
                detail = "Tài khoản cần hoàn tất lời mời (đặt mật khẩu mới).",
                requiresInvitationCompletion = true
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

            SetAuthCookies(response, identity.TenantCode, identity.UserType);

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
            ClearAuthCookies();
            return Unauthorized(new { detail = "Refresh token invalid or expired." });
        }
    }

    /// <summary>Đăng xuất: revoke best-effort phía server + xóa cookies.</summary>
    [HttpPost("logout")]
    [Authorize]
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

        // Luôn xóa cookies phía client dù server revoke thất bại
        ClearAuthCookies();
        return NoContent();
    }

    // --- Cookie helpers ---

    private void SetAuthCookies(LoginResponse response, string tenantCode, string userType)
    {
        Response.Cookies.Append(AccessTokenCookie, response.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(response.ExpiresIn)
        });

        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            Response.Cookies.Append(RefreshTokenCookie, response.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = RefreshCookiePath, // chỉ gửi kèm cho /api/v1/auth/*
                MaxAge = TimeSpan.FromDays(30)
            });

            Response.Cookies.Append(TenantCodeCookie, tenantCode, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = RefreshCookiePath,
                MaxAge = TimeSpan.FromDays(30)
            });

            Response.Cookies.Append(UserTypeCookie, userType, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = RefreshCookiePath,
                MaxAge = TimeSpan.FromDays(30)
            });
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
}
