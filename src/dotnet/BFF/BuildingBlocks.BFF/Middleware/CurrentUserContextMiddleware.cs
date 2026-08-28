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
            // Custom claims (user_id, tenant_id) — được thêm bởi OnTokenValidated
            // hoặc bởi một middleware riêng resolve từ gRPC IdentifyUser
            var userId      = GetClaimGuid(context.User, JwtClaims.UserId);
            var tenantId    = GetClaimGuid(context.User, JwtClaims.TenantId);
            var permVersion = GetClaimInt(context.User, JwtClaims.PermissionVersion);
            var traceId     = context.TraceIdentifier;

            var role = context.User.FindFirstValue(JwtClaims.Role)
                ?? context.User.FindFirstValue(ClaimTypes.Role);

            // Permissions sẽ được load từ Redis bởi PermissionVersionMiddleware (bước tiếp theo)
            currentUser.Populate(userId, tenantId, traceId, permVersion, role, []);
        }

        await next(context);
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
