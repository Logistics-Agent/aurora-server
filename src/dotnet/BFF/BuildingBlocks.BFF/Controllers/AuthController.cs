using System.Security.Claims;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.BFF.Controllers;

/// <summary>
/// Auth endpoints cho Cognito Hosted UI flow.
/// Được đăng ký tự động ở tất cả micro-BFF thông qua AddControllers().
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IOptions<CognitoAuthOptions> cognitoOptions) : ControllerBase
{
    private readonly CognitoAuthOptions _cognito = cognitoOptions.Value;

    /// <summary>
    /// Redirect user sang Cognito Hosted UI (login page).
    /// Frontend gọi: GET /api/v1/auth/login?returnUrl=/dashboard
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = "/")
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(returnUrl ?? "/");

        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/"
        };

        return Challenge(props, AuthExtensions.CognitoScheme);
    }

    /// <summary>
    /// Logout: clear cookie session + redirect sang Cognito logout endpoint.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout([FromQuery] string? returnUrl = "/")
    {
        HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var logoutUrl = $"{_cognito.LogoutEndpoint}" +
            $"?client_id={_cognito.ClientId}" +
            $"&logout_uri={Uri.EscapeDataString(returnUrl ?? _cognito.AppDomain)}";

        return Redirect(logoutUrl);
    }

    /// <summary>
    /// GET /api/v1/auth/logout — hỗ trợ logout bằng GET cho redirect từ frontend.
    /// </summary>
    [HttpGet("logout")]
    [Authorize]
    public IActionResult LogoutGet([FromQuery] string? returnUrl = "/")
        => Logout(returnUrl);

    /// <summary>
    /// Trả về thông tin user hiện tại từ cookie session.
    /// Không cần gọi DB — đọc trực tiếp từ claims đã lưu trong cookie.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var email = User.FindFirstValue("email")
                 ?? User.FindFirstValue(ClaimTypes.Email);

        return Ok(new
        {
            Email = email,
            EmailDomain = User.FindFirstValue("email_domain"),
            CognitoSub = User.FindFirstValue("sub")
                       ?? User.FindFirstValue("cognito_sub"),
            UserId = User.FindFirstValue("user_id"),
            TenantId = User.FindFirstValue("tenant_id"),
            Name = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("name")
                ?? User.FindFirstValue("cognito:username"),
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
