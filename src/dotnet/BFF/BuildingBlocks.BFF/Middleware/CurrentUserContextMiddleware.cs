using System.Security.Claims;
using Shared.Security;

namespace BuildingBlocks.BFF.Middleware;

/// <summary>
/// Populate ICurrentUserService từ ClaimsPrincipal (cookie session).
/// Claims được enrich bởi OnTokenValidated event trong AuthExtensions (email, email_domain).
/// Custom claims (user_id, tenant_id) cần được thêm sau khi gRPC IdentifyUser hoàn tất.
/// Phải chạy SAU UseAuthentication() và TokenRefreshMiddleware.
/// </summary>
public class CurrentUserContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUser)
    {
        // Chỉ populate khi user đã được authenticate
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = GetClaimGuid(context.User, "user_id")
                      ?? GetClaimGuid(context.User, ClaimTypes.NameIdentifier);
            var tenantId = GetClaimGuid(context.User, "tenant_id");
            var traceId = context.TraceIdentifier;
            var permVersion = GetClaimInt(context.User, "permission_version");
            var role = context.User.FindFirstValue(ClaimTypes.Role)
                    ?? context.User.FindFirstValue("role");

            // Custom claims (user_id, tenant_id) — được thêm bởi OnTokenValidated
            // Nếu cookie chưa có userId (do login từ session cũ), fallback resolve từ AuthService
            if (!userId.HasValue)
            {
                var email = context.User.FindFirstValue(ClaimTypes.Email)
                         ?? context.User.FindFirstValue("email");

                if (!string.IsNullOrWhiteSpace(email))
                {
                    try
                    {
                        var authClient = context.RequestServices.GetService<Auth.Grpc.AuthService.AuthServiceClient>();
                        if (authClient != null)
                        {
                            var identity = await authClient.IdentifyUserAsync(
                                new Auth.Grpc.IdentifyUserRequest { Email = email },
                                cancellationToken: context.RequestAborted);

                            if (identity != null && identity.Exists && Guid.TryParse(identity.UserId, out var resolvedUserId))
                            {
                                userId = resolvedUserId;
                                permVersion = identity.PermissionVersion;
                                if (Guid.TryParse(identity.TenantId, out var resolvedTenantId))
                                    tenantId = resolvedTenantId;
                                if (!string.IsNullOrWhiteSpace(identity.Role))
                                    role = identity.Role;
                            }
                        }
                    }
                    catch
                    {
                        // Proceed with available claims if service unreachable
                    }
                }
            }

            // Đồng bộ role và claims vào ClaimsIdentity để ASP.NET Core [Authorize(Roles = "...")] nhận diện được
            if (context.User.Identity is ClaimsIdentity claimsIdentity)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    var canonicalRole = role.Trim().ToUpperInvariant() switch
                    {
                        "SYSTEMADMIN" or "SYSTEM_ADMIN" => Shared.Constants.RoleConstants.SystemAdmin,
                        "TENANTADMIN" or "TENANT_ADMIN" => Shared.Constants.RoleConstants.TenantAdmin,
                        "MANAGER" => Shared.Constants.RoleConstants.Manager,
                        _ => role
                    };

                    role = canonicalRole;

                    EnsureClaim(claimsIdentity, ClaimTypes.Role, canonicalRole);
                    EnsureClaim(claimsIdentity, "role", canonicalRole);
                    EnsureClaim(claimsIdentity, "cognito:groups", canonicalRole);
                    EnsureClaim(claimsIdentity, Shared.Security.JwtClaims.Role, canonicalRole);
                    if (!string.IsNullOrWhiteSpace(claimsIdentity.RoleClaimType) && claimsIdentity.RoleClaimType != ClaimTypes.Role)
                    {
                        EnsureClaim(claimsIdentity, claimsIdentity.RoleClaimType, canonicalRole);
                    }
                }

                if (userId.HasValue)
                {
                    EnsureClaim(claimsIdentity, "user_id", userId.Value.ToString());
                    EnsureClaim(claimsIdentity, Shared.Security.JwtClaims.UserId, userId.Value.ToString());
                    EnsureClaim(claimsIdentity, ClaimTypes.NameIdentifier, userId.Value.ToString());
                }

                if (tenantId.HasValue)
                {
                    EnsureClaim(claimsIdentity, "tenant_id", tenantId.Value.ToString());
                    EnsureClaim(claimsIdentity, Shared.Security.JwtClaims.TenantId, tenantId.Value.ToString());
                }

                if (permVersion.HasValue)
                {
                    EnsureClaim(claimsIdentity, "permission_version", permVersion.Value.ToString());
                    EnsureClaim(claimsIdentity, Shared.Security.JwtClaims.PermissionVersion, permVersion.Value.ToString());
                }
            }

            // Permissions sẽ được load từ Redis bởi PermissionVersionMiddleware (bước tiếp theo)
            currentUser.Populate(userId, tenantId, traceId, permVersion, role, []);
        }

        await next(context);
    }

    private static void EnsureClaim(ClaimsIdentity identity, string claimType, string claimValue)
    {
        var existing = identity.FindAll(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (existing.Count == 0 || existing.All(c => !string.Equals(c.Value, claimValue, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var c in existing)
            {
                identity.RemoveClaim(c);
            }
            identity.AddClaim(new Claim(claimType, claimValue));
        }
    }

    private static Guid? GetClaimGuid(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var result) ? result : null;
    }

    private static int? GetClaimInt(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return int.TryParse(value, out var result) ? result : null;
    }
}

