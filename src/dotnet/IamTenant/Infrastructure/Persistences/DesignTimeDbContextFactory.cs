using System;
using System.Collections.Generic;
using IamTenant.Infrastructure.Persistences;
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
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                    ?? "Host=localhost;Port=5432;Database=aurora_mail_service;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
        npgsql.MigrationsAssembly(typeof(IamTenantDbContext).Assembly.FullName);
        });

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
    public string? Role => "STAFF";
    public IReadOnlyList<string> Permissions => [];

    public void Populate(Guid? userId, Guid? tenantId, string? traceId, int? permissionVersion, string? role, List<string> permissions) { }
    public void PopulatePermissions(List<string> permissions, string? role) { }
}
