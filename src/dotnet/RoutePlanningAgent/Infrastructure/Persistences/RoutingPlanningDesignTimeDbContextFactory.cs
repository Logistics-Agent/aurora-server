using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RoutePlanningAgent.Infrastructure.Persistence;

public class RoutingPlanningDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoutePlanningDbContext>
{
    public RoutePlanningDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RoutePlanningDbContext>();

        // Design-time fallback connection string for schema extraction & migration generation
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=aurora_mail_service;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(RoutePlanningDbContext).Assembly.FullName);
        });

        // Design-time dummy ICurrentUserService (system/migration context)
        var dummyUser = new DesignTimeCurrentUserService();

        var auditInterceptor = new AuditSaveChangesInterceptor(dummyUser);

        return new RoutePlanningDbContext(optionsBuilder.Options, dummyUser, auditInterceptor);
    }

    private class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public string? TraceId => string.Empty;
        public int? PermissionVersion => 1;
        public string? Role => null;
        public IReadOnlyList<string> Permissions => Array.Empty<string>();
    }
}
