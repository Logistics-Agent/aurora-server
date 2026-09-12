using System.Security.Claims;
using BuildingBlocks.BFF.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Tests;

public sealed class TenantContextMiddlewareTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid ClaimedTenantId = Guid.CreateVersion7();
    private static readonly Guid RequestedTenantId = Guid.CreateVersion7();

    [Fact]
    public async Task Tenant_user_ignores_header_and_query_tenant_overrides()
    {
        var currentUser = new CurrentUserService();
        var context = CreateContext(RoleConstants.Staff, ClaimedTenantId);
        context.Request.Headers["x-tenant-id"] = RequestedTenantId.ToString();
        context.Request.QueryString = new QueryString($"?tenantId={RequestedTenantId}");

        await InvokeCurrentUserMiddleware(context, currentUser);

        Assert.Equal(ClaimedTenantId, currentUser.TenantId);
        Assert.Equal(ClaimedTenantId.ToString(), context.User.FindFirstValue(JwtClaims.TenantId));
    }

    [Fact]
    public async Task Tenant_user_without_trusted_tenant_fails_closed_for_document_routes()
    {
        var currentUser = new CurrentUserService();
        var context = CreateContext(RoleConstants.Staff, tenantId: null);
        context.Request.Path = "/api/v1/documents/uploads";
        context.Request.Headers["x-tenant-id"] = RequestedTenantId.ToString();
        var nextCalled = false;

        await InvokeCurrentUserMiddleware(context, currentUser);
        await new TenantResolutionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance)
            .InvokeAsync(context, currentUser);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Null(currentUser.TenantId);
    }

    [Fact]
    public async Task System_admin_requires_explicit_override_header_before_selecting_tenant()
    {
        var currentUser = new CurrentUserService();
        var context = CreateContext(RoleConstants.SystemAdmin, tenantId: null);
        context.Request.Headers["x-tenant-id"] = RequestedTenantId.ToString();

        await InvokeCurrentUserMiddleware(context, currentUser);

        Assert.Null(currentUser.TenantId);
    }

    [Fact]
    public async Task System_admin_can_select_tenant_only_with_explicit_override_header()
    {
        var currentUser = new CurrentUserService();
        var context = CreateContext(RoleConstants.SystemAdmin, tenantId: null);
        context.Request.Headers["x-tenant-id"] = RequestedTenantId.ToString();
        context.Request.Headers["x-tenant-context-override"] = "true";

        await InvokeCurrentUserMiddleware(context, currentUser);

        Assert.Equal(RequestedTenantId, currentUser.TenantId);
        Assert.Equal(RequestedTenantId.ToString(), context.User.FindFirstValue(JwtClaims.TenantId));
    }

    [Fact]
    public async Task System_admin_without_override_is_allowed_to_reach_system_scope_routes()
    {
        var currentUser = new CurrentUserService();
        var context = CreateContext(RoleConstants.SystemAdmin, tenantId: null);
        context.Request.Path = "/api/v1/system/tenants";
        var nextCalled = false;

        await InvokeCurrentUserMiddleware(context, currentUser);
        await new TenantResolutionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance)
            .InvokeAsync(context, currentUser);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static async Task InvokeCurrentUserMiddleware(
        DefaultHttpContext context,
        ICurrentUserContext currentUser)
    {
        await new CurrentUserContextMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(context, currentUser);
    }

    private static DefaultHttpContext CreateContext(string role, Guid? tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId.ToString()),
            new("user_id", UserId.ToString()),
            new(ClaimTypes.Email, "staff@example.test"),
            new("email", "staff@example.test"),
            new(ClaimTypes.Role, role),
            new("role", role),
            new("permission_version", "1")
        };
        if (tenantId.HasValue)
            claims.Add(new Claim(JwtClaims.TenantId, tenantId.Value.ToString()));

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
    }
}
