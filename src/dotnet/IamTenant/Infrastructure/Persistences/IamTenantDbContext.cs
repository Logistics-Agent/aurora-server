using Shared.Entity;
using Shared.Enums;
using Shared.Interceptors;
using Shared.Security;
using IamTenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Infrastructure.Persistences;

public class IamTenantDbContext(
    DbContextOptions<IamTenantDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly Guid? _tenantId = currentUser.TenantId;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasQueryFilter(u =>
                _tenantId.HasValue && u.TenantId == _tenantId.Value
                && !u.IsDeleted);

            e.HasIndex(u => u.CognitoSub).IsUnique();
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.HasIndex(u => new { u.TenantId, u.CreatedAt });
            e.HasIndex(u => new { u.TenantId, u.Status });

            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.CognitoSub).HasMaxLength(128);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(50);
        });

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasQueryFilter(t => !t.IsDeleted);
            e.HasIndex(t => t.Code).IsUnique();
            e.HasIndex(t => t.CompanyDomain).IsUnique();
            e.HasIndex(t => t.CreatedAt);
            e.HasIndex(t => new { t.Status, t.CreatedAt });

            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Code).HasMaxLength(50).IsRequired();
            e.Property(t => t.CompanyDomain).HasMaxLength(100).IsRequired();
            e.Property(t => t.TaxCode).HasMaxLength(50);
            e.Property(t => t.PlanType);
            e.Property(t => t.Status);
            e.Property(t => t.AdminGroupId).HasMaxLength(128);
            e.Property(t => t.UserGroupId).HasMaxLength(128);
            e.Property(t => t.AdminUserPoolId).HasMaxLength(128);
            e.Property(t => t.AdminUserPoolClientId).HasMaxLength(128);
            e.Property(t => t.StaffUserPoolId).HasMaxLength(128);
            e.Property(t => t.StaffUserPoolClientId).HasMaxLength(128);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(100).IsRequired();
            e.Property(p => p.Module).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<UserPermission>(e =>
        {
            e.HasKey(up => new { up.UserId, up.PermissionId });
            e.HasIndex(up => new { up.TenantId, up.UserId });

            e.HasOne(up => up.User)
                .WithMany(u => u.UserPermissions)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(up => up.Permission)
                .WithMany()
                .HasForeignKey(up => up.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.Property(o => o.EventType).HasMaxLength(256).IsRequired();
            e.Property(o => o.Payload).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Actor).HasMaxLength(128).IsRequired();
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.Resource).HasMaxLength(200).IsRequired();
            e.Property(a => a.CorrelationId).HasMaxLength(128);
        });

        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed All Authoritative Permissions
        SeedSystemData(modelBuilder);
    }

    private static void SeedSystemData(ModelBuilder modelBuilder)
    {
        var allCodes = Shared.Constants.PermissionConstants.GetAllPermissions();
        var codeToId = allCodes.ToDictionary(code => code, DeterministicPermissionId);

        modelBuilder.Entity<Permission>().HasData(
            allCodes.Select(code => new Permission
            {
                Id = codeToId[code],
                Code = code,
                Module = code.Split(':')[0],
                Description = $"Allows capability {code}"
            }).ToArray()
        );
    }

    /// <summary>
    /// Sinh GUID ổn định từ permission code (MD5) cho HasData.
    /// </summary>
    public static Guid DeterministicPermissionId(string code)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"iam-permission:{code}"));
        return new Guid(hash);
    }
}
