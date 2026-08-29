using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.Security;

namespace MailService.Infrastructure.Persistence;

public class MailServiceDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MailServiceDbContext>
{
    public MailServiceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MailServiceDbContext>();

        // Design-time fallback connection string for schema extraction & migration generation
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=aurora_mail_service;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(MailServiceDbContext).Assembly.FullName);
        });

        // Design-time dummy ICurrentUserService (system/migration context)
        var dummyUser = new DesignTimeCurrentUserService();

        return new MailServiceDbContext(optionsBuilder.Options, dummyUser);
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
