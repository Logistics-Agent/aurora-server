using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.Interceptors;
using Shared.Security;

namespace IamTenant.Infrastructure.Persistences;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IamTenantDbContext>
{
    public IamTenantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IamTenantDbContext>();
        optionsBuilder.UseNpgsql("DefaultConnectionString",
            npgsql => npgsql.MigrationsAssembly("IamTenant"));

        var mockUser = new DummyCurrentUserService();
        var mockInterceptor = new AuditSaveChangesInterceptor(mockUser);

        return new IamTenantDbContext(optionsBuilder.Options, mockUser, mockInterceptor);
    }
}

internal class DummyCurrentUserService : ICurrentUserContext
{
    public Guid? UserId => Guid.Empty;
    public Guid? TenantId => Guid.Empty;
    public string? TraceId => null;
    public int? PermissionVersion => 1;
    public IReadOnlyList<string> RoleIds => [];
    public IReadOnlyList<string> Permissions => [];

    public void Populate(Guid? userId, Guid? tenantId, string? traceId, int? permissionVersion, List<string> roleIds, List<string> permissions) { }
    public void PopulatePermissions(List<string> permissions, List<string> roleIds) { }
}
