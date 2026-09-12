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
            var explicitUserId = GetClaimGuid(context.User, "user_id") 
                              ?? GetClaimGuid(context.User, Shared.Security.JwtClaims.UserId)
                              ?? GetClaimGuid(context.User, "custom:user_id");
            var userId = explicitUserId ?? GetClaimGuid(context.User, ClaimTypes.NameIdentifier);
            var tenantId = GetClaimGuid(context.User, "tenant_id") 
                        ?? GetClaimGuid(context.User, Shared.Security.JwtClaims.TenantId)
                        ?? GetClaimGuid(context.User, "custom:tenant_id");

            var traceId = context.TraceIdentifier;
            var permVersion = GetClaimInt(context.User, "permission_version") 
                           ?? GetClaimInt(context.User, Shared.Security.JwtClaims.PermissionVersion);
            var groupClaims = context.User.FindAll("cognito:groups").Select(c => c.Value).ToList();
            var role = context.User.FindFirstValue(ClaimTypes.Role)
                    ?? context.User.FindFirstValue("role")
                    ?? context.User.FindFirstValue("custom:role")
                    ?? context.User.FindFirstValue(Shared.Security.JwtClaims.Role)
                    ?? groupClaims.FirstOrDefault();

            // Custom claims (user_id, tenant_id, permission_version)
            // Nếu thiếu userId thực tế trong DB hoặc thiếu tenantId / permVersion (vd: khi xác thực bằng raw Cognito access_token), fallback resolve từ AuthService
            if (!explicitUserId.HasValue || !tenantId.HasValue || !permVersion.HasValue || string.IsNullOrWhiteSpace(role))
            {
                var email = context.User.FindFirstValue(ClaimTypes.Email)
                         ?? context.User.FindFirstValue("email")
                         ?? context.User.FindFirstValue("username")
                         ?? context.User.FindFirstValue("cognito:username")
                         ?? context.User.FindFirstValue("sub")
                         ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

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

            role = NormalizeRole(role);
            if (IsExplicitSystemAdminTenantOverride(context, role, out var overrideTenantId))
                tenantId = overrideTenantId;

            // Đồng bộ role và claims vào ClaimsIdentity để ASP.NET Core [Authorize(Roles = "...")] nhận diện được
            if (context.User.Identity is ClaimsIdentity claimsIdentity)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    EnsureClaim(claimsIdentity, ClaimTypes.Role, role);
                    EnsureClaim(claimsIdentity, "role", role);
                    EnsureClaim(claimsIdentity, "cognito:groups", role);
                    EnsureClaim(claimsIdentity, Shared.Security.JwtClaims.Role, role);
                    if (!string.IsNullOrWhiteSpace(claimsIdentity.RoleClaimType) && claimsIdentity.RoleClaimType != ClaimTypes.Role)
                    {
                        EnsureClaim(claimsIdentity, claimsIdentity.RoleClaimType, role);
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

    private static string? NormalizeRole(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "SYSTEMADMIN" or "SYSTEM_ADMIN" => Shared.Constants.RoleConstants.SystemAdmin,
        "TENANTADMIN" or "TENANT_ADMIN" => Shared.Constants.RoleConstants.TenantAdmin,
        "MANAGER" => Shared.Constants.RoleConstants.Manager,
        "STAFF" => Shared.Constants.RoleConstants.Staff,
        _ => role
    };

    private static bool IsExplicitSystemAdminTenantOverride(
        HttpContext context,
        string? role,
        out Guid tenantId)
    {
        tenantId = Guid.Empty;
        if (!string.Equals(role, Shared.Constants.RoleConstants.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
            !context.Request.Headers.TryGetValue("x-tenant-context-override", out var overrideHeader) ||
            !string.Equals(overrideHeader.ToString(), "true", StringComparison.OrdinalIgnoreCase) ||
            !context.Request.Headers.TryGetValue("x-tenant-id", out var tenantHeader) ||
            !Guid.TryParse(tenantHeader.ToString(), out tenantId) ||
            tenantId == Guid.Empty)
        {
            tenantId = Guid.Empty;
            return false;
        }

        return true;
    }
}
