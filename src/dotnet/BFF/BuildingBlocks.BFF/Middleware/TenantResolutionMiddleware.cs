using Shared.Constants;
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

        // TenantId phải có trong JWT/IdentifyUser context. Request headers/query không phải nguồn tenant.
        // SystemAdmin chỉ được chạy system-scope routes khi không có tenant; tenant-scoped routes fail closed.
        if (context.User.Identity?.IsAuthenticated == true && !currentUser.TenantId.HasValue &&
            !string.Equals(currentUser.Role, RoleConstants.SystemAdmin, StringComparison.OrdinalIgnoreCase) &&
            !IsSystemRoute(path))
        {
            logger.LogWarning(
                "Authenticated user {UserId} missing trusted TenantId. Rejecting tenant-scoped path {Path}.",
                currentUser.UserId, path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.io/401",
                title = "TENANT_CONTEXT_REQUIRED",
                status = StatusCodes.Status401Unauthorized,
                detail = "A trusted tenant context is required for this resource.",
                code = "TENANT_CONTEXT_REQUIRED",
                retryable = false,
                traceId = context.TraceIdentifier
            }, context.RequestAborted);
            return;
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

    private static bool IsSystemRoute(string path) =>
        path.StartsWith("/api/v1/system", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/system", StringComparison.OrdinalIgnoreCase);
}
