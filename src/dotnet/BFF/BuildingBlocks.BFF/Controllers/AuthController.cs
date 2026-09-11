using System.Security.Claims;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Security;

namespace BuildingBlocks.BFF.Controllers;

/// <summary>
/// Auth endpoints cho Cognito Hosted UI flow.
/// Được đăng ký tự động ở tất cả micro-BFF thông qua AddControllers().
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IOptions<CognitoAuthOptions> cognitoOptions,
    ICurrentUserService currentUserService) : ControllerBase
{
    private readonly CognitoAuthOptions _cognito = cognitoOptions.Value;
    private readonly ICurrentUserService _currentUser = currentUserService;

    /// <summary>
    /// Redirect user sang Cognito Hosted UI (login page).
    /// Frontend gọi: GET /api/v1/auth/login?returnUrl=/dashboard
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var targetUrl = ResolveReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
            return Redirect(targetUrl);

        var props = new AuthenticationProperties
        {
            RedirectUri = targetUrl
        };

        return Challenge(props, AuthExtensions.CognitoScheme);
    }

    /// <summary>
    /// Callback từ Cognito Hosted UI sau khi authenticate thành công.
    /// Redirect về returnUrl (mặc định: /swagger nếu không có returnUrl).
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public IActionResult Callback([FromQuery] string? returnUrl = null)
    {
        var targetUrl = ResolveReturnUrl(returnUrl);
        return Redirect(targetUrl);
    }

    /// <summary>
    /// POST /api/v1/auth/logout — sign out khỏi cookie session và chuyển tiếp sang Cognito logout endpoint nếu có cấu hình.
    /// </summary>
    [HttpPost("logout")]
    [HttpGet("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var targetUrl = ResolveReturnUrl(returnUrl);

        // Nếu Cognito Domain chưa được cấu hình hoặc là placeholder mặc định (không phân giải được DNS), redirect thẳng về targetUrl
        if (string.IsNullOrWhiteSpace(_cognito.Domain) ||
            string.IsNullOrWhiteSpace(_cognito.ClientId) ||
            _cognito.Domain.Equals("aurora-platform-demo", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(targetUrl);
        }

        var fullLogoutUri = targetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? targetUrl
            : $"{Request.Scheme}://{Request.Host}{targetUrl}";

        var logoutUrl = $"{_cognito.LogoutEndpoint}" +
            $"?client_id={_cognito.ClientId}" +
            $"&logout_uri={Uri.EscapeDataString(fullLogoutUri)}";

        return Redirect(logoutUrl);
    }

    private string ResolveReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl != "/")
            return returnUrl;

        if (Request.Headers.TryGetValue("Referer", out var referer) && !string.IsNullOrWhiteSpace(referer))
        {
            var refererStr = referer.ToString();
            // Tránh loop lại chính trang login/logout
            if (!refererStr.Contains("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) &&
                !refererStr.Contains("/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase))
            {
                return refererStr;
            }
        }

        return "/swagger";
    }

    /// <summary>
    /// Trả về thông tin user hiện tại từ auth context: Persona Role + N Direct Permissions.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var email = User.FindFirstValue("email")
                 ?? User.FindFirstValue(ClaimTypes.Email);

        var cognitoSub = User.FindFirstValue("cognito_sub")
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("cognito:username");

        var role = _currentUser.Role ?? User.FindFirstValue(JwtClaims.Role) ?? User.FindFirstValue(ClaimTypes.Role) ?? RoleConstants.Staff;
        var directPermissions = _currentUser.Permissions ?? [];
        var permissions = directPermissions.Count > 0
            ? directPermissions
            : role.Trim().ToUpperInvariant() switch
            {
                "MANAGER" => PermissionConstants.GetDefaultManagerPermissions(),
                "TENANT_ADMIN" or "TENANTADMIN" => PermissionConstants.GetTenantAdminPermissions(),
                "SYSTEM_ADMIN" or "SYSTEMADMIN" => PermissionConstants.GetAllPermissions(),
                _ => PermissionConstants.GetDefaultStaffPermissions()
            };

        return Ok(new
        {
            Email = email,
            EmailDomain = User.FindFirstValue("email_domain")
                       ?? (email != null && email.Contains('@') ? email.Split('@')[1] : null),
            CognitoSub = cognitoSub,
            UserId = _currentUser.UserId?.ToString() ?? User.FindFirstValue(JwtClaims.UserId) ?? User.FindFirstValue("user_id"),
            TenantId = _currentUser.TenantId?.ToString() ?? User.FindFirstValue(JwtClaims.TenantId) ?? User.FindFirstValue("tenant_id"),
            Role = role,
            Permissions = permissions,
            Name = User.FindFirstValue("name")
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? (email != null && email.Contains('@') ? email.Split('@')[0] : null)
                ?? cognitoSub,
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// Login failed — redirect khi Cognito trả error.
    /// </summary>
    [HttpGet("login-failed")]
    [AllowAnonymous]
    public IActionResult LoginFailed()
    {
        return Unauthorized(new
        {
            Type = "https://httpstatuses.io/401",
            Title = "Authentication failed",
            Detail = "Login was unsuccessful. Please try again.",
            Status = 401
        });
    }
}
