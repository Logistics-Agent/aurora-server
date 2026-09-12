using Shared.Security;

namespace BuildingBlocks.BFF.Middleware;

/// <summary>
/// Validate TenantId từ JWT claims (Backend là Source of Truth).
/// TenantId đã được set trong CurrentUserContextMiddleware từ ClaimsPrincipal.
/// Middleware này validate và log cảnh báo nếu authenticated user thiếu TenantId.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        // Public routes không cần tenant context
        var path = context.Request.Path.Value ?? "";
        if (IsPublicPath(path))
        {
            await next(context);
            return;
        }

        // TenantId phải có trong JWT claims (đã được populate bởi CurrentUserContextMiddleware)
        // SystemAdmin có thể không có TenantId (TenantId = null = system scope)
        if (context.User.Identity?.IsAuthenticated == true && !currentUser.TenantId.HasValue)
        {
            logger.LogWarning(
                "Authenticated user {UserId} missing TenantId claim. Path: {Path}. " +
                "This is expected only for SystemAdmin accounts.",
                currentUser.UserId, path);
        }

        await next(context);
    }

    private static bool IsPublicPath(string path) =>
        // Auth endpoints thực tế nằm dưới /api/v{n}/auth/... (literal cũ /auth/login không bao giờ match)
        (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && path.Contains("/auth/", StringComparison.OrdinalIgnoreCase)) ||
        path.StartsWith("/healthz",  StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/metrics",  StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api-docs", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger",  StringComparison.OrdinalIgnoreCase);
}
