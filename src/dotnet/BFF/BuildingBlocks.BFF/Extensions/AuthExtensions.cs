using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;

namespace BuildingBlocks.BFF.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Đăng ký JWT Bearer (đọc từ HttpOnly Cookie) + Authorization.
    /// Lưu ý Cognito:
    /// - Access token của Cognito KHÔNG có claim "aud" → ValidateAudience = false,
    ///   thay vào đó validate claim "client_id" trong OnTokenValidated.
    /// - Role claim đọc từ "cognito:groups" (cấu hình được qua Auth:Jwt:RoleClaimType) —
    ///   groups phải được provision theo role codes: SYSTEM_ADMIN, TENANT_ADMIN, TENANT_STAFF.
    /// - Các custom claims (user_id, tenant_id, role_ids, permission_version — xem JwtClaims)
    ///   cần Pre Token Generation lambda phía Cognito.
    /// </summary>
    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var expectedClientId = config["Auth:Jwt:Audience"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = config["Auth:Jwt:Authority"];
                opts.RequireHttpsMetadata = config.GetValue("Auth:CookieSecure", true);

                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    // Cognito access token không có "aud" — validate client_id thủ công bên dưới
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = config["Auth:Jwt:RoleClaimType"] ?? "cognito:groups"
                };

                // Đọc token từ HttpOnly Cookie thay vì Authorization header
                opts.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Cookies["access_token"];
                        if (!string.IsNullOrWhiteSpace(token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ctx =>
                    {
                        // Validate client_id thay cho audience (Cognito access token)
                        if (!string.IsNullOrEmpty(expectedClientId))
                        {
                            var clientId = ctx.Principal?.FindFirst("client_id")?.Value;
                            if (clientId != expectedClientId)
                            {
                                ctx.Fail("Invalid client_id.");
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // ICurrentUserService — Scoped theo request
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

        return services;
    }
}
