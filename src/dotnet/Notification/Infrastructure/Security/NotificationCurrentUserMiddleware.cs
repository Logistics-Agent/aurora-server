using System.Security.Claims;
using Shared.Security;

namespace Notification.Infrastructure.Security;

public sealed class NotificationCurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            currentUser.Populate(ParseGuid(context.User, JwtClaims.UserId), ParseGuid(context.User, JwtClaims.TenantId), context.TraceIdentifier, null, [], []);
        }
        await next(context);
    }

    private static Guid? ParseGuid(ClaimsPrincipal principal, string claim) => Guid.TryParse(principal.FindFirstValue(claim), out var value) ? value : null;
}
