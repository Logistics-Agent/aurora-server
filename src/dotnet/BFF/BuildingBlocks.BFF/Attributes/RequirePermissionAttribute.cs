using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Security;

namespace BuildingBlocks.BFF.Attributes;

/// <summary>
/// Requires the current authenticated user to possess a specific capability permission.
/// Supports optional endpoint-specific legacy fallback permissions for non-breaking migrations.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string RequiredPermission { get; }
    public string? LegacyFallbackPermission { get; }

    public RequirePermissionAttribute(string permission)
    {
        RequiredPermission = permission;
        LegacyFallbackPermission = null;
    }

    public RequirePermissionAttribute(string permissionOrModule, string legacyFallbackOrAction)
    {
        if (permissionOrModule.Contains(':'))
        {
            RequiredPermission = permissionOrModule;
            LegacyFallbackPermission = legacyFallbackOrAction;
        }
        else
        {
            RequiredPermission = $"{permissionOrModule}:{legacyFallbackOrAction}";
            LegacyFallbackPermission = null;
        }
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 1. If endpoint has [AllowAnonymous], skip
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em.GetType() == typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute)))
        {
            return Task.CompletedTask;
        }

        // 2. Resolve ICurrentUserService
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        // 3. If unauthenticated
        if (!currentUser.UserId.HasValue)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        // 4. SYSTEM_ADMIN bypass (Super Admin)
        if (currentUser.RoleIds != null && currentUser.RoleIds.Any(r => string.Equals(r, RoleConstants.SystemAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        // 5. Capability Permission Check
        var permissions = currentUser.Permissions ?? (IReadOnlyList<string>)Array.Empty<string>();
        bool hasPermission = permissions.Contains(RequiredPermission);

        // Optional legacy fallback with audit logging
        if (!hasPermission && !string.IsNullOrWhiteSpace(LegacyFallbackPermission) && permissions.Contains(LegacyFallbackPermission))
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<RequirePermissionAttribute>>();
            logger?.LogWarning(
                "Legacy authorization fallback: User {UserId} accessed {Path} using deprecated permission '{Legacy}' instead of capability '{Required}'.",
                currentUser.UserId, context.HttpContext.Request.Path, LegacyFallbackPermission, RequiredPermission);

            hasPermission = true;
        }

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new { detail = $"Missing required permission: {RequiredPermission}" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
