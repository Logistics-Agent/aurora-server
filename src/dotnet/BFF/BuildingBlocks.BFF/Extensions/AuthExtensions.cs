using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using BuildingBlocks.BFF.Options;
using Shared.Security;

namespace BuildingBlocks.BFF.Extensions;

/// <summary>
/// Marker class cho logger category trong auth events.
/// </summary>
public class BffAuthEvents;

public static class AuthExtensions
{
    public const string CognitoScheme = "Cognito";

    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        // ── Bind options ─────────────────────────────────────────────────────
        var cognitoOpts = config.GetSection(CognitoAuthOptions.SectionName).Get<CognitoAuthOptions>()
            ?? config.GetSection("Auth:Cognito").Get<CognitoAuthOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section '{CognitoAuthOptions.SectionName}'");

        var cookieOpts = config.GetSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>()
            ?? new AuthCookieOptions();

        services.Configure<CognitoAuthOptions>(config.GetSection(CognitoAuthOptions.SectionName));
        services.Configure<AuthCookieOptions>(config.GetSection(AuthCookieOptions.SectionName));
        services.Configure<AuthCookieConfig>(config.GetSection(AuthCookieOptions.SectionName)); // compatibility

        // ── Authentication: Cookie (default) + OpenIdConnect (Cognito) ───────
        services
            .AddAuthentication(opts =>
            {
                opts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = CognitoScheme;
            })
            .AddCookie(opts =>
            {
                opts.Cookie.Name = ".Aurora.Auth";
                opts.Cookie.HttpOnly = true;
                opts.Cookie.SecurePolicy = cookieOpts.Secure
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                opts.Cookie.SameSite = SameSiteMode.Lax;

                if (!string.IsNullOrWhiteSpace(cookieOpts.Domain))
                    opts.Cookie.Domain = cookieOpts.Domain;

                opts.ExpireTimeSpan = TimeSpan.FromMinutes(cookieOpts.SessionTimeoutMinutes);
                opts.SlidingExpiration = true;

                // Trả 401 JSON thay vì redirect khi gọi API mà chưa auth
                opts.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api") &&
                        !ctx.Request.Path.StartsWithSegments("/api/v1/auth"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };

                opts.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(CognitoScheme, opts =>
            {
                opts.Authority = cognitoOpts.Authority;
                opts.ClientId = cognitoOpts.ClientId;
                opts.ClientSecret = cognitoOpts.ClientSecret;

                opts.ResponseType = OpenIdConnectResponseType.Code;
                opts.UsePkce = true;
                opts.SaveTokens = true; // Lưu access_token, refresh_token, id_token vào cookie

                opts.CallbackPath = "/api/v1/auth/callback";
                opts.SignedOutCallbackPath = "/api/v1/auth/signout-callback";

                // Scopes — Cognito yêu cầu scope "openid", "profile", "email"
                opts.Scope.Clear();
                opts.Scope.Add("openid");
                opts.Scope.Add("profile");
                opts.Scope.Add("email");

                // Cognito discovery endpoint
                opts.MetadataAddress = $"{cognitoOpts.Authority}/.well-known/openid-configuration";

                opts.TokenValidationParameters.ValidateIssuer = true;
                opts.TokenValidationParameters.ValidIssuer = cognitoOpts.Authority;

                opts.Events = new OpenIdConnectEvents
                {
                    // ── Sau khi token validated: split email domain → resolve tenant ──
                    OnTokenValidated = async ctx =>
                    {
                        var identity = ctx.Principal?.Identity as ClaimsIdentity;
                        if (identity is null) return;

                        // Cognito trả email trong claim "email" hoặc ClaimTypes.Email
                        var email = ctx.Principal?.FindFirstValue("email")
                                 ?? ctx.Principal?.FindFirstValue(ClaimTypes.Email);

                        if (string.IsNullOrWhiteSpace(email))
                        {
                            ctx.Fail("Email claim not found in Cognito token.");
                            return;
                        }

                        // Split email domain (sau @) — dùng để resolve tenant
                        var emailDomain = email.Contains('@') ? email.Split('@')[1] : string.Empty;

                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<BffAuthEvents>>();

                        logger.LogInformation(
                            "Cognito token validated for email {Email}, domain {Domain}. Resolving tenant...",
                            email, emailDomain);

                        identity.AddClaim(new Claim("email_domain", emailDomain));

                        var cognitoSub = ctx.Principal?.FindFirstValue("sub");
                        if (!string.IsNullOrWhiteSpace(cognitoSub))
                            identity.AddClaim(new Claim("cognito_sub", cognitoSub));

                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Email))
                            identity.AddClaim(new Claim(ClaimTypes.Email, email));
                    },

                    // ── Handle Cognito remote errors ──────────────────────────────
                    OnRemoteFailure = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<BffAuthEvents>>();

                        if (ctx.Failure?.Message?.Contains("access_denied") == true)
                        {
                            logger.LogInformation("Cognito login cancelled by user.");
                            ctx.Response.Redirect("/");
                            ctx.HandleResponse();
                            return Task.CompletedTask;
                        }

                        logger.LogError(ctx.Failure, "Cognito remote authentication failure.");
                        ctx.Response.Redirect("/api/v1/auth/login-failed");
                        ctx.HandleResponse();
                        return Task.CompletedTask;
                    },

                    // ── Redirect to Cognito logout endpoint ──────────────────────
                    OnRedirectToIdentityProviderForSignOut = ctx =>
                    {
                        var logoutUri = $"{cognitoOpts.LogoutEndpoint}" +
                            $"?client_id={cognitoOpts.ClientId}" +
                            $"&logout_uri={Uri.EscapeDataString(cognitoOpts.AppDomain)}";

                        ctx.Response.Redirect(logoutUri);
                        ctx.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // ── ICurrentUserService — Scoped theo request ────────────────────────
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

        // ── HttpClient cho token refresh ─────────────────────────────────────
        services.AddHttpClient("CognitoTokenRefresh");

        return services;
    }
}
