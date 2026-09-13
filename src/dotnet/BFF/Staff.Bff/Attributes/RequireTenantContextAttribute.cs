using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Shared.Security;
using StaffBff.Services;

namespace StaffBff.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireTenantContextAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => -1000;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
        {
            context.Result = new UnauthorizedObjectResult(
                DocumentsContract.CreateTenantContextRequiredProblemDetails());
        }

        return Task.CompletedTask;
    }
}
