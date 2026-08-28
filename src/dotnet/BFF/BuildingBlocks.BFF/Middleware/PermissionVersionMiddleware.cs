using Shared.Cache;
using Shared.Security;

namespace BuildingBlocks.BFF.Middleware;

/// <summary>
/// Kiểm tra PermissionVersion trong JWT (cookie) có khớp với version đang lưu trong Redis không.
/// Nếu version khớp → load permissions từ Redis vào ICurrentUserContext.
/// Nếu version lệch → reject 401 (admin vừa thay đổi quyền user, client phải login lại).
/// Nếu không có cache entry → reject 401 (session expired hoặc chưa được warm-up).
/// </summary>
public class PermissionVersionMiddleware(
    RequestDelegate next,
    ILogger<PermissionVersionMiddleware> logger)
{
    // IPermissionCacheService là scoped service → phải inject qua InvokeAsync,
    // không được inject qua constructor (middleware là singleton).
    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUser, IPermissionCacheService permissionCache)
    {
        // Chỉ kiểm tra với các request authenticated và có UserId + PermissionVersion
        if (context.User.Identity?.IsAuthenticated == true
            && currentUser.UserId.HasValue
            && currentUser.PermissionVersion.HasValue)
        {
            var cached = await permissionCache.GetAsync(currentUser.UserId.Value);

            // Không có cache entry — yêu cầu login lại để repopulate Redis
            if (cached is null)
            {
                logger.LogWarning(
                    "No permission cache entry for User {UserId}. Forcing re-authentication. Path: {Path}",
                    currentUser.UserId, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    Type   = "https://httpstatuses.io/401",
                    Title  = "Session expired",
                    Detail = "Your session has expired. Please log in again.",
                    Status = 401,
                    TraceId = context.TraceIdentifier
                });
                return;
            }

            // Version lệch — admin đã thay đổi quyền
            if (cached.Version != currentUser.PermissionVersion.Value)
            {
                logger.LogWarning(
                    "PermissionVersion mismatch for User {UserId}. JWT={JwtVersion}, Cache={CacheVersion}. Rejecting.",
                    currentUser.UserId, currentUser.PermissionVersion, cached.Version);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    Type   = "https://httpstatuses.io/401",
                    Title  = "Token expired",
                    Detail = "Your permissions have been updated. Please log in again.",
                    Status = 401,
                    TraceId = context.TraceIdentifier
                });
                return;
            }

            // ✅ Version khớp — load permissions từ Redis vào user context
            currentUser.PopulatePermissions(cached.Permissions, cached.Role);

            logger.LogDebug(
                "Permissions loaded for User {UserId}: {PermissionCount} permissions, role {Role}, version {Version}.",
                currentUser.UserId, cached.Permissions.Count, cached.Role, cached.Version);
        }

        await next(context);
    }
}
