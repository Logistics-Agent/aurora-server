using System.Globalization;
using System.Text;
using System.Text.Json;
using BuildingBlocks.BFF.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.BFF.Middleware;

/// <summary>
/// Auto-refresh access_token khi sắp hết hạn.
/// Đọc expires_at từ auth cookie properties (SaveTokens=true).
/// Nếu access_token sắp hết hạn (< buffer) → dùng refresh_token gọi Cognito /oauth2/token endpoint.
/// Thành công → cập nhật cookie session với tokens mới.
/// Thất bại → sign out, trả 401.
/// Phải chạy SAU UseAuthentication().
/// </summary>
public class TokenRefreshMiddleware(
    RequestDelegate next,
    IHttpClientFactory httpClientFactory,
    IOptions<CognitoAuthOptions> cognitoOptions,
    IOptions<AuthCookieOptions> cookieOptions,
    ILogger<TokenRefreshMiddleware> logger)
{
    private readonly CognitoAuthOptions _cognito = cognitoOptions.Value;
    private readonly AuthCookieOptions _cookie = cookieOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        // Chỉ xử lý với authenticated users
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // Đọc expires_at từ auth properties
        var expiresAtStr = await context.GetTokenAsync("expires_at");
        if (string.IsNullOrWhiteSpace(expiresAtStr) ||
            !DateTimeOffset.TryParse(expiresAtStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt))
        {
            await next(context);
            return;
        }

        var buffer = TimeSpan.FromMinutes(_cookie.TokenRefreshBufferMinutes);

        // Token vẫn còn valid → skip
        if (DateTimeOffset.UtcNow + buffer < expiresAt)
        {
            await next(context);
            return;
        }

        // ── Token sắp hết hạn → refresh ─────────────────────────────────────
        var refreshToken = await context.GetTokenAsync("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            logger.LogWarning("Access token expiring but no refresh_token available. Signing out.");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        logger.LogInformation("Access token expiring at {ExpiresAt}. Refreshing...", expiresAt);

        try
        {
            var newTokens = await RefreshTokensAsync(refreshToken);

            if (newTokens is null)
            {
                logger.LogWarning("Token refresh failed. Signing out.");
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // Cập nhật tokens trong auth cookie
            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult.Properties is not null)
            {
                authResult.Properties.UpdateTokenValue("access_token", newTokens.AccessToken);
                if (!string.IsNullOrWhiteSpace(newTokens.IdToken))
                    authResult.Properties.UpdateTokenValue("id_token", newTokens.IdToken);
                
                if (!string.IsNullOrWhiteSpace(newTokens.RefreshToken))
                    authResult.Properties.UpdateTokenValue("refresh_token", newTokens.RefreshToken);
                    
                authResult.Properties.UpdateTokenValue("expires_at",
                    newTokens.ExpiresAt.ToString("o", CultureInfo.InvariantCulture));

                // Re-sign cookie với tokens mới
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    authResult.Principal!,
                    authResult.Properties);

                logger.LogInformation("Token refreshed successfully. New expiry: {ExpiresAt}", newTokens.ExpiresAt);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during token refresh. Signing out.");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Gọi Cognito /oauth2/token endpoint với grant_type=refresh_token.
    /// Cognito yêu cầu Basic Auth header (client_id:client_secret) nếu có client_secret.
    /// </summary>
    private async Task<TokenRefreshResult?> RefreshTokensAsync(string refreshToken)
    {
        var client = httpClientFactory.CreateClient("CognitoTokenRefresh");

        if (!string.IsNullOrWhiteSpace(_cognito.ClientSecret))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_cognito.ClientId}:{_cognito.ClientSecret}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _cognito.ClientId,
            ["refresh_token"] = refreshToken
        };

        var response = await client.PostAsync(
            _cognito.TokenEndpoint,
            new FormUrlEncodedContent(requestBody));

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Cognito token refresh failed: {StatusCode} - {Body}",
                response.StatusCode, errorBody);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        return new TokenRefreshResult
        {
            AccessToken = accessToken,
            IdToken = root.TryGetProperty("id_token", out var id) ? id.GetString() : null,
            RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
        };
    }

    private class TokenRefreshResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
