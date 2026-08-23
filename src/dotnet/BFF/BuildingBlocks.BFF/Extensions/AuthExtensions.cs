using System.Security.Claims;
using BuildingBlocks.BFF.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Shared.Security;

namespace BuildingBlocks.BFF.Extensions;

/// <summary>
/// Marker class cho logger category trong auth events.
/// </summary>
public class BffAuthEvents;

public static class AuthExtensions
{
    public const string CognitoScheme = OpenIdConnectDefaults.AuthenticationScheme;

    /// <summary>
    /// Đăng ký cookie session + OpenIdConnect (Cognito) + Authorization.
    /// Session được lưu trong HttpOnly cookie, còn OIDC xử lý đăng nhập và callback.
    /// Các custom claims (user_id, tenant_id, role_ids, permission_version — xem JwtClaims)
    /// cần Pre Token Generation lambda phía Cognito.
    /// </summary>
    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var cognitoOpts = config.GetSection(CognitoAuthOptions.SectionName).Get<CognitoAuthOptions>()
            ?? new CognitoAuthOptions();
        var cookieOpts = config.GetSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>()
            ?? new AuthCookieOptions();
        var expectedClientId = config["Auth:Jwt:Audience"];
        var roleClaimType = config["Auth:Jwt:RoleClaimType"] ?? "cognito:groups";

        services.Configure<CognitoAuthOptions>(config.GetSection(CognitoAuthOptions.SectionName));
        services.Configure<AuthCookieOptions>(config.GetSection(AuthCookieOptions.SectionName));
        services.Configure<AuthCookieConfig>(config.GetSection(AuthCookieOptions.SectionName)); // compatibility

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CognitoScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = ".Aurora.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = cookieOpts.Secure
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;

                if (!string.IsNullOrWhiteSpace(cookieOpts.Domain))
                    options.Cookie.Domain = cookieOpts.Domain;

                options.ExpireTimeSpan = TimeSpan.FromMinutes(cookieOpts.SessionTimeoutMinutes);
                options.SlidingExpiration = true;
                options.ReturnUrlParameter = string.Empty;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(CognitoScheme, options =>
            {
                options.Authority = cognitoOpts.Authority;
                options.ClientId = cognitoOpts.ClientId;
                options.ClientSecret = cognitoOpts.ClientSecret;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = true;

                options.CallbackPath = "/api/v1/auth/callback";
                options.SignedOutCallbackPath = "/api/v1/auth/signout-callback";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.MetadataAddress = $"{cognitoOpts.Authority}/.well-known/openid-configuration";

                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidIssuer = cognitoOpts.Authority;
                options.TokenValidationParameters.ValidateAudience = false;
                options.TokenValidationParameters.RoleClaimType = roleClaimType;
                options.TokenValidationParameters.NameClaimType = ClaimTypes.Email;

                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        var identity = context.Principal?.Identity as ClaimsIdentity;
                        if (identity is null)
                            return Task.CompletedTask;

                        var email = context.Principal?.FindFirstValue("email")
                            ?? context.Principal?.FindFirstValue(ClaimTypes.Email);

                        if (string.IsNullOrWhiteSpace(email))
                        {
                            context.Fail("Email claim not found in Cognito token.");
                            return Task.CompletedTask;
                        }

                        var emailDomain = email.Contains('@') ? email.Split('@')[1] : string.Empty;
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<BffAuthEvents>>();

                        logger.LogInformation(
                            "Cognito token validated for email {Email}, domain {Domain}. Resolving tenant...",
                            email,
                            emailDomain);

                        if (!identity.HasClaim(c => c.Type == "email_domain"))
                            identity.AddClaim(new Claim("email_domain", emailDomain));

                        var cognitoSub = context.Principal?.FindFirstValue("sub");
                        if (!string.IsNullOrWhiteSpace(cognitoSub) && !identity.HasClaim(c => c.Type == "cognito_sub"))
                            identity.AddClaim(new Claim("cognito_sub", cognitoSub));

                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Email))
                            identity.AddClaim(new Claim(ClaimTypes.Email, email));

                        if (!string.IsNullOrWhiteSpace(expectedClientId))
                        {
                            var clientId = context.Principal?.FindFirst("client_id")?.Value;
                            if (!string.Equals(clientId, expectedClientId, StringComparison.Ordinal))
                                context.Fail("Invalid client_id.");
                        }

                        return Task.CompletedTask;
                    },
                    OnRemoteFailure = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<BffAuthEvents>>();

                        if (context.Failure?.Message?.Contains("access_denied", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            logger.LogInformation("Cognito login cancelled by user.");
                            context.Response.Redirect("/");
                            context.HandleResponse();
                            return Task.CompletedTask;
                        }

                        logger.LogError(context.Failure, "Cognito remote authentication failure.");
                        context.Response.Redirect("/api/v1/auth/login-failed");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        var logoutUri = $"{cognitoOpts.LogoutEndpoint}" +
                            $"?client_id={cognitoOpts.ClientId}" +
                            $"&logout_uri={Uri.EscapeDataString(cognitoOpts.AppDomain)}";

                        context.Response.Redirect(logoutUri);
                        context.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

        services.AddHttpClient("CognitoTokenRefresh");

        return services;
    }
}