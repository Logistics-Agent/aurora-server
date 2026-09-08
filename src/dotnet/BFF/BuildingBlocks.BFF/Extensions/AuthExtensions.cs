using System.Security.Claims;
using BuildingBlocks.BFF.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Shared.Extensions;
using Shared.Security;
using StackExchange.Redis;

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
    /// Các custom claims (user_id, tenant_id, role, permission_version — xem JwtClaims)
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

        // Shared Data Protection across all BFFs (Staff.Bff, Admin.Bff, System.Bff)
        try
        {
            var redisOptions = SharedServiceExtensions.BuildRedisConfigurationOptions(config);
            var redis = ConnectionMultiplexer.Connect(redisOptions);
            services.AddSingleton<IConnectionMultiplexer>(redis);

            services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(redis, "aurora:dataprotection-keys")
                .SetApplicationName("Aurora.BFF");
        }
        catch (Exception ex)
        {
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger(nameof(AuthExtensions));
            logger.LogWarning(ex, "Failed to connect to Redis for DataProtection.");
            services.AddDataProtection()
                .SetApplicationName("Aurora.BFF");
        }

        services.Configure<CognitoAuthOptions>(config.GetSection(CognitoAuthOptions.SectionName));
        services.Configure<AuthCookieOptions>(config.GetSection(AuthCookieOptions.SectionName));
        services.Configure<AuthCookieConfig>(config.GetSection(AuthCookieOptions.SectionName)); // compatibility

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                var sameSiteMode = cookieOpts.SameSite?.Equals("None", StringComparison.OrdinalIgnoreCase) == true
                    ? SameSiteMode.None
                    : cookieOpts.SameSite?.Equals("Strict", StringComparison.OrdinalIgnoreCase) == true
                        ? SameSiteMode.Strict
                        : SameSiteMode.Lax;

                options.Cookie.Name = ".Aurora.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = sameSiteMode;
                options.Cookie.SecurePolicy = (sameSiteMode == SameSiteMode.None || cookieOpts.Secure)
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;

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
                    OnRedirectToIdentityProvider = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api") &&
                            !context.Request.Path.StartsWithSegments("/api/v1/auth/login"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.HandleResponse();
                            return Task.CompletedTask;
                        }

                        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
                                         ?? context.Request.Host.Value;

                        var isLocal = forwardedHost?.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) == true
                                   || forwardedHost?.StartsWith("127.0.0.1") == true;
                        var scheme = isLocal ? "http" : "https";

                        context.ProtocolMessage.RedirectUri = $"{scheme}://{forwardedHost ?? context.Request.Host.Value}{options.CallbackPath}";
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var identity = context.Principal?.Identity as ClaimsIdentity;
                        if (identity is null)
                            return;

                        var email = context.Principal?.FindFirstValue("email")
                            ?? context.Principal?.FindFirstValue(ClaimTypes.Email);

                        if (string.IsNullOrWhiteSpace(email))
                        {
                            context.Fail("Email claim not found in Cognito token.");
                            return;
                        }

                        var emailDomain = email.Contains('@') ? email.Split('@')[1] : string.Empty;
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<BffAuthEvents>>();

                        logger.LogInformation(
                            "Cognito token validated for email {Email}, domain {Domain}. Resolving tenant...",
                            email,
                            emailDomain);

                        if (!identity.HasClaim(c => c.Type == "email_domain"))
                            identity.AddClaim(new Claim("email_domain", emailDomain));

                        var cognitoSub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                                      ?? context.Principal?.FindFirstValue("sub")
                                      ?? context.Principal?.FindFirstValue("cognito_sub")
                                      ?? context.Principal?.FindFirstValue("cognito:username");

                        if (!string.IsNullOrWhiteSpace(cognitoSub))
                        {
                            if (!identity.HasClaim(c => c.Type == "cognito_sub"))
                                identity.AddClaim(new Claim("cognito_sub", cognitoSub));
                            if (!identity.HasClaim(c => c.Type == "sub"))
                                identity.AddClaim(new Claim("sub", cognitoSub));
                        }

                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Email))
                            identity.AddClaim(new Claim(ClaimTypes.Email, email));

                        // Map Cognito Groups / Role claim to canonical Role
                        var groupClaims = context.Principal?.FindAll("cognito:groups").Select(c => c.Value).ToList() ?? [];
                        var rawRole = context.Principal?.FindFirstValue("role") 
                                   ?? context.Principal?.FindFirstValue("custom:role")
                                   ?? groupClaims.FirstOrDefault();

                        var canonicalRole = Shared.Constants.RoleConstants.Staff;
                        if (!string.IsNullOrWhiteSpace(rawRole))
                        {
                            canonicalRole = rawRole.Trim().ToUpperInvariant() switch
                            {
                                "SYSTEMADMIN" or "SYSTEM_ADMIN" => Shared.Constants.RoleConstants.SystemAdmin,
                                "TENANTADMIN" or "TENANT_ADMIN" => Shared.Constants.RoleConstants.TenantAdmin,
                                "MANAGER" => Shared.Constants.RoleConstants.Manager,
                                _ => Shared.Constants.RoleConstants.Staff
                            };
                        }

                        // Resolve internal user details (UserId, TenantId, PermissionVersion) from IamTenant service
                        try
                        {
                            var authClient = context.HttpContext.RequestServices.GetService<Auth.Grpc.AuthService.AuthServiceClient>();
                            var iamClient = context.HttpContext.RequestServices.GetService<IamTenant.Grpc.IamService.IamServiceClient>();

                            if (authClient != null)
                            {
                                var identityResp = await authClient.IdentifyUserAsync(
                                    new Auth.Grpc.IdentifyUserRequest { Email = email });

                                if (identityResp != null && identityResp.Exists)
                                {
                                    if (!string.IsNullOrWhiteSpace(identityResp.Role))
                                        canonicalRole = identityResp.Role;

                                    if (!string.IsNullOrWhiteSpace(identityResp.UserId))
                                    {
                                        if (!identity.HasClaim(c => c.Type == Shared.Security.JwtClaims.UserId))
                                            identity.AddClaim(new Claim(Shared.Security.JwtClaims.UserId, identityResp.UserId));

                                        if (!identity.HasClaim(c => c.Type == Shared.Security.JwtClaims.PermissionVersion))
                                            identity.AddClaim(new Claim(Shared.Security.JwtClaims.PermissionVersion, identityResp.PermissionVersion.ToString()));

                                        if (!string.IsNullOrWhiteSpace(identityResp.TenantId) && !identity.HasClaim(c => c.Type == Shared.Security.JwtClaims.TenantId))
                                            identity.AddClaim(new Claim(Shared.Security.JwtClaims.TenantId, identityResp.TenantId));

                                        // Pre-warm Redis cache with direct permissions
                                        if (iamClient != null)
                                        {
                                            try
                                            {
                                                await iamClient.GetUserPermissionsAsync(
                                                    new IamTenant.Grpc.GetUserPermissionsRequest { UserId = identityResp.UserId });
                                            }
                                            catch (Exception ex)
                                            {
                                                logger.LogWarning(ex, "Failed to pre-warm permission cache for user {UserId}", identityResp.UserId);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to resolve user identity during token validation for {Email}", email);
                        }

                        if (!identity.HasClaim(c => c.Type == Shared.Security.JwtClaims.Role))
                            identity.AddClaim(new Claim(Shared.Security.JwtClaims.Role, canonicalRole));
                        if (!identity.HasClaim(c => c.Type == ClaimTypes.Role))
                            identity.AddClaim(new Claim(ClaimTypes.Role, canonicalRole));

                        if (!string.IsNullOrWhiteSpace(expectedClientId))
                        {
                            var clientId = context.Principal?.FindFirst("client_id")?.Value
                                        ?? context.Principal?.FindFirst("aud")?.Value;
                            if (!string.Equals(clientId, expectedClientId, StringComparison.Ordinal))
                                context.Fail("Invalid client_id.");
                        }
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