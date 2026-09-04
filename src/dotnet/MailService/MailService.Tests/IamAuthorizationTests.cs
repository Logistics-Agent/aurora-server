using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.BFF.Attributes;
using IamTenant.Application.Commands.Permissions;
using IamTenant.Application.Commands.Tenants;
using IamTenant.Application.Commands.Users;
using IamTenant.Application.DTOs.Roles;
using IamTenant.Application.Interfaces;
using IamTenant.Application.Queries.Permissions;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Cache;
using Shared.Constants;
using Shared.Enums;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using Xunit;

namespace MailService.Tests;

public class IamAuthorizationTests
{
    private static IamTenantDbContext CreateTestDbContext(string dbName, ICurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<IamTenantDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var auditInterceptor = new AuditSaveChangesInterceptor(currentUser);
        var context = new IamTenantDbContext(options, currentUser, auditInterceptor);
        context.Database.EnsureCreated();
        return context;
    }

    private static (Guid TenantId, Tenant Tenant) SeedTenant(IamTenantDbContext context, string domain = "testcorp.vn")
    {
        var tenantId = Guid.NewGuid();
        var tenant = Tenant.Create("Test Corp", domain, "123456789", Guid.NewGuid());
        tenant.Id = tenantId;
        tenant.AdminUserPoolId = "admin-pool-1";
        tenant.AdminUserPoolClientId = "admin-client-1";
        tenant.StaffUserPoolId = "user-pool-1";
        tenant.StaffUserPoolClientId = "user-client-1";
        context.Tenants.Add(tenant);
        context.SaveChanges();
        return (tenantId, tenant);
    }

    [Fact]
    public async Task CreateStaff_AssignsSingleBaseRole_AndSeedsDefaultTemplatePermissions()
    {
        var dbName = Guid.NewGuid().ToString();
        var adminUserId = Guid.NewGuid();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object));

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(adminUserId);
        mockCurrentUser.Setup(u => u.Role).Returns(RoleConstants.TenantAdmin);

        var mockCognito = new Mock<ICognitoAuthService>();
        mockCognito.Setup(c => c.AdminCreateUserInPoolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cognito-sub-alice");

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);
        var handler = new CreateStaffHandler(context, mockCurrentUser.Object, mockCognito.Object);

        var command = new CreateStaffCommand(
            Email: "alice@testcorp.vn",
            FirstName: "Alice",
            LastName: "Smith",
            Role: RoleConstants.Staff,
            ApplyDefaultPermissions: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(RoleConstants.Staff, result.Role);
        Assert.NotEmpty(result.Permissions);
        Assert.Contains(PermissionConstants.Mail.Read, result.Permissions);
        Assert.Contains(PermissionConstants.RoutePlanning.Read, result.Permissions);
        Assert.DoesNotContain(PermissionConstants.RoutePlanning.PolicyPublish, result.Permissions); // Staff does NOT get publish by default

        // Verify DB state
        var userInDb = await context.Users
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Email == "alice@testcorp.vn");

        Assert.NotNull(userInDb);
        Assert.Equal(BaseRole.Staff, userInDb.Role);
        Assert.Equal(result.Permissions.Count, userInDb.UserPermissions.Count);
    }

    [Fact]
    public async Task CreateStaff_RejectsSystemAdminAssignment_InTenantContext()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object));

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockCognito = new Mock<ICognitoAuthService>();

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);
        var handler = new CreateStaffHandler(context, mockCurrentUser.Object, mockCognito.Object);

        var command = new CreateStaffCommand(
            Email: "badactor@testcorp.vn",
            FirstName: "Bad",
            LastName: "Actor",
            Role: RoleConstants.SystemAdmin);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Cannot assign SYSTEM_ADMIN role within a tenant context", ex.Message);
    }

    [Fact]
    public async Task CreateStaff_RejectsGrantingSystemOnlyPermissions_ByTenantAdmin()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object));

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockCognito = new Mock<ICognitoAuthService>();

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);
        var handler = new CreateStaffHandler(context, mockCurrentUser.Object, mockCognito.Object);

        var command = new CreateStaffCommand(
            Email: "bob@testcorp.vn",
            FirstName: "Bob",
            LastName: "Vance",
            Role: RoleConstants.Staff,
            Permissions: [PermissionConstants.Mail.SystemManage]); // System-only!

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Cannot grant platform system-only permissions", ex.Message);
    }

    [Fact]
    public async Task UpdateUserRole_StaffToManager_AppliesDefaultTemplateUnion_AndInvalidatesCache()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object));

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockCognito = new Mock<ICognitoAuthService>();
        mockCognito.Setup(c => c.AdminCreateUserInPoolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cognito-sub-charlie");

        var mockCache = new Mock<IPermissionCacheService>();

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);
        var createHandler = new CreateStaffHandler(context, mockCurrentUser.Object, mockCognito.Object);

        var created = await createHandler.Handle(new CreateStaffCommand(
            "charlie@testcorp.vn", "Charlie", "Brown", RoleConstants.Staff, ApplyDefaultPermissions: true), CancellationToken.None);

        var updateRoleHandler = new UpdateUserRoleHandler(context, mockCurrentUser.Object, mockCache.Object);

        var updateResult = await updateRoleHandler.Handle(new UpdateUserRoleCommand(
            created.Id, RoleConstants.Manager, ApplyDefaultPermissions: true), CancellationToken.None);

        Assert.Equal(RoleConstants.Manager, updateResult.Role);
        Assert.Contains(PermissionConstants.RoutePlanning.PolicyPublish, updateResult.Permissions);
        Assert.Contains(PermissionConstants.Mail.ThreadReassign, updateResult.Permissions);
        Assert.Equal(2, updateResult.PermissionVersion);

        // Redis cache invalidation called
        mockCache.Verify(c => c.InvalidateAsync(created.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserRole_ManagerToStaffDowngrade_DetectsElevatedPermissionsRetained()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object));

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockCognito = new Mock<ICognitoAuthService>();
        mockCognito.Setup(c => c.AdminCreateUserInPoolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cognito-sub-david");

        var mockCache = new Mock<IPermissionCacheService>();

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);
        var createHandler = new CreateStaffHandler(context, mockCurrentUser.Object, mockCognito.Object);

        // Create as Manager
        var created = await createHandler.Handle(new CreateStaffCommand(
            "david@testcorp.vn", "David", "Miller", RoleConstants.Manager, ApplyDefaultPermissions: true), CancellationToken.None);

        var updateRoleHandler = new UpdateUserRoleHandler(context, mockCurrentUser.Object, mockCache.Object);

        // Downgrade to STAFF without auto-revoking
        var updateResult = await updateRoleHandler.Handle(new UpdateUserRoleCommand(
            created.Id, RoleConstants.Staff, ApplyDefaultPermissions: false), CancellationToken.None);

        Assert.Equal(RoleConstants.Staff, updateResult.Role);
        // Elevated permissions detected for Admin visibility
        Assert.NotEmpty(updateResult.ElevatedPermissionsRetained);
        Assert.Contains(PermissionConstants.RoutePlanning.PolicyPublish, updateResult.ElevatedPermissionsRetained);
        Assert.Contains(PermissionConstants.RoutePlanning.Approve, updateResult.ElevatedPermissionsRetained);
    }

    [Fact]
    public async Task UpdateUserPermissions_DeltaGrantAndRevoke_AtomicallyUpdatesAndInvalidatesCache()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object));

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockCognito = new Mock<ICognitoAuthService>();
        mockCognito.Setup(c => c.AdminCreateUserInPoolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cognito-sub-eve");

        var mockCache = new Mock<IPermissionCacheService>();

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);
        var createHandler = new CreateStaffHandler(context, mockCurrentUser.Object, mockCognito.Object);

        var created = await createHandler.Handle(new CreateStaffCommand(
            "eve@testcorp.vn", "Eve", "Adams", RoleConstants.Staff, ApplyDefaultPermissions: true), CancellationToken.None);

        var updatePermHandler = new UpdateUserPermissionsHandler(context, mockCurrentUser.Object, mockCache.Object);

        // Grant specific permission and revoke baseline permission
        var result = await updatePermHandler.Handle(new UpdateUserPermissionsCommand(
            created.Id,
            Grant: [PermissionConstants.RoutePlanning.PolicyPublish],
            Revoke: [PermissionConstants.Mail.Send]), CancellationToken.None);

        var grantedCodes = result.Permissions.Select(p => p.Code).ToList();
        Assert.Contains(PermissionConstants.RoutePlanning.PolicyPublish, grantedCodes);
        Assert.DoesNotContain(PermissionConstants.Mail.Send, grantedCodes);
        Assert.Equal(2, result.Version);

        // Redis cache invalidation called
        mockCache.Verify(c => c.InvalidateAsync(created.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateUserPermissions_EnforcesStrictTenantIsolation()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var (tenantAId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object), "tenanta.vn");
        var (tenantBId, _) = SeedTenant(CreateTestDbContext(dbName, mockCurrentUser.Object), "tenantb.vn");

        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantAId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockCognito = new Mock<ICognitoAuthService>();
        mockCognito.Setup(c => c.AdminCreateUserInPoolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cognito-sub-test");

        var mockCache = new Mock<IPermissionCacheService>();

        using var context = CreateTestDbContext(dbName, mockCurrentUser.Object);

        // User A in Tenant A
        var userA = new User
        {
            TenantId = tenantAId,
            Email = "usera@tenanta.vn",
            Role = BaseRole.Staff
        };
        // User B in Tenant B
        var userB = new User
        {
            TenantId = tenantBId,
            Email = "userb@tenantb.vn",
            Role = BaseRole.Staff
        };
        context.Users.AddRange(userA, userB);
        await context.SaveChangesAsync();

        var bulkHandler = new BulkUpdateUserPermissionsHandler(context, mockCurrentUser.Object, mockCache.Object);

        // Attempting to include userB (from tenant B) in tenant A's bulk update MUST throw DomainException (fail-closed!)
        var ex = await Assert.ThrowsAsync<DomainException>(() => bulkHandler.Handle(
            new BulkUpdateUserPermissionsCommand([userA.Id, userB.Id], Grant: [PermissionConstants.RoutePlanning.Optimize]),
            CancellationToken.None));

        Assert.Contains("belong to another tenant", ex.Message);
    }

    [Fact]
    public async Task RequirePermissionAttribute_EnforcesPureCapability_ZeroRoleBypass()
    {
        // Setup CurrentUserService with SYSTEM_ADMIN role BUT missing required capability
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mockUser.Setup(u => u.Role).Returns(RoleConstants.SystemAdmin);
        mockUser.Setup(u => u.Permissions).Returns([PermissionConstants.Mail.Read]);

        var services = new ServiceCollection();
        services.AddSingleton(mockUser.Object);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var filterContext = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        var attribute = new RequirePermissionAttribute(PermissionConstants.RoutePlanning.PolicyPublish);

        await attribute.OnAuthorizationAsync(filterContext);

        // Result MUST be 403 Forbidden even if user is SystemAdmin persona! Zero role bypasses!
        Assert.NotNull(filterContext.Result);
        var objectResult = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
}
