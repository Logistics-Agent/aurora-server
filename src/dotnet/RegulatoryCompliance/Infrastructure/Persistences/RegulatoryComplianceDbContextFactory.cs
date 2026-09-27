using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Infrastructure.Persistences;

public sealed class RegulatoryComplianceDbContextFactory
    : IDesignTimeDbContextFactory<RegulatoryComplianceDbContext>
{
    public RegulatoryComplianceDbContext CreateDbContext(string[] args)
    {
        var currentUser = new CurrentUserService();
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=regulatory_compliance_design_time",
                npgsql => npgsql.MigrationsAssembly("RegulatoryCompliance").UseVector())
            .Options;
        return new RegulatoryComplianceDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }
}
