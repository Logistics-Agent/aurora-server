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
