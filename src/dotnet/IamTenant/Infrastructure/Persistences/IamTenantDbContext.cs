using Shared.Entity;
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
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
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

        // ============================================================
        // GLOBAL QUERY FILTERS — tham chiếu trực tiếp qua _currentUser
        // (KHÔNG dùng local variable - EF Core sẽ capture đúng per-request)
        // ============================================================

        modelBuilder.Entity<User>(e =>
        {
            e.HasQueryFilter(u =>
                (!_currentUser.TenantId.HasValue || u.TenantId == _currentUser.TenantId)
                && !u.IsDeleted);

            e.HasIndex(u => u.CognitoSub).IsUnique();
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.HasIndex(u => new { u.TenantId, u.CreatedAt });
            e.HasIndex(u => new { u.TenantId, u.Status });

            // MaxLength để tránh Postgres tạo ra cột 'text'
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.CognitoSub).HasMaxLength(128);
            // UserType + StaffType lưu int (ordinal) theo quyết định thiết kế — chỉ append, không reorder
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
            e.Property(t => t.PlanType).HasMaxLength(50);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);
        });

        modelBuilder.Entity<Role>(e =>
        {
            // Filter: Role hệ thống (IsSystemRole = true) hiện ra cho mọi tenant
            e.HasQueryFilter(r =>
                r.IsSystemRole
                || !_currentUser.TenantId.HasValue
                || r.TenantId == _currentUser.TenantId);

            e.HasIndex(r => new { r.TenantId, r.Code }).IsUnique();
            e.Property(r => r.Code).HasMaxLength(100).IsRequired();
            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(100).IsRequired();
            e.Property(p => p.Module).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
        });

        // Composite PKs cho junction tables
        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });
        modelBuilder.Entity<UserPermission>(e =>
        {
            e.HasKey(up => new { up.UserId, up.PermissionId });
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

        // ------------------------------------------------------------
        // RESTRICT CASCADE DELETE CHO TENANT
        // ------------------------------------------------------------
        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Roles)
            .WithOne(r => r.Tenant)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // ============================================================
        // SEED: System Roles & Permissions mặc định
        // ============================================================
        SeedSystemData(modelBuilder);
    }

    private static void SeedSystemData(ModelBuilder modelBuilder)
    {
        // System Tenant (tenantId = Guid.Empty = system scope)
        var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // --- Roles ---
        var sysAdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var tenantAdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var tenantStaffRoleId = Guid.Parse("10000000-0000-0000-0000-000000000003");

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = sysAdminRoleId, TenantId = systemTenantId, Code = "SYSTEM_ADMIN", Name = "System Administrator", IsSystemRole = true, CreatedAt = DateTimeOffset.UnixEpoch, CreatedBy = "system" },
            new Role { Id = tenantAdminRoleId, TenantId = systemTenantId, Code = "TENANT_ADMIN", Name = "Tenant Administrator", IsSystemRole = true, CreatedAt = DateTimeOffset.UnixEpoch, CreatedBy = "system" },
            new Role { Id = tenantStaffRoleId, TenantId = systemTenantId, Code = "TENANT_STAFF", Name = "Tenant Staff", IsSystemRole = true, CreatedAt = DateTimeOffset.UnixEpoch, CreatedBy = "system" }
        );

        // --- Permissions: seed ĐÚNG bộ codes của PermissionConstants ("{module}:{action}") ---
        // [RequirePermission] ở BFF build code từ PermissionConstants — seed lệch bộ này là 403 toàn bộ.
        var allCodes = Shared.Constants.PermissionConstants.GetAllPermissions();
        var codeToId = allCodes.ToDictionary(code => code, DeterministicPermissionId);

        modelBuilder.Entity<Permission>().HasData(
            allCodes.Select(code => new Permission
            {
                Id = codeToId[code],
                Code = code,
                Module = code.Split(':')[0],
                Description = $"Allows {code.Split(':')[1]} operation on {code.Split(':')[0]}"
            }).ToArray()
        );

        // --- RolePermissions ---
        // SYSTEM_ADMIN: toàn bộ
        var sysAdminPerms = allCodes.Select(code => new RolePermission
        {
            RoleId = sysAdminRoleId,
            PermissionId = codeToId[code]
        });

        // TENANT_ADMIN: toàn quyền iam:* + route_planning:*
        var tenantAdminPerms = allCodes
            .Where(code => code.StartsWith("iam:") || code.StartsWith("route_planning:"))
            .Select(code => new RolePermission
            {
                RoleId = tenantAdminRoleId,
                PermissionId = codeToId[code]
            });

        // TENANT_STAFF: quyền staff mặc định (read/export/import) + route_planning:create
        var staffCodes = Shared.Constants.PermissionConstants.GetDefaultStaffPermissions()
            .Append(Shared.Constants.PermissionConstants.Build(
                Shared.Constants.PermissionConstants.Modules.RoutePlanning,
                Shared.Constants.PermissionConstants.Create))
            .Distinct();

        var tenantStaffPerms = staffCodes.Select(code => new RolePermission
        {
            RoleId = tenantStaffRoleId,
            PermissionId = codeToId[code]
        });

        modelBuilder.Entity<RolePermission>().HasData(
            [.. sysAdminPerms, .. tenantAdminPerms, .. tenantStaffPerms]);
    }

    /// <summary>
    /// Sinh GUID ổn định từ permission code (MD5) — bắt buộc cho HasData
    /// (giá trị phải giống nhau giữa các lần build model, nếu không migration sẽ churn).
    /// </summary>
    private static Guid DeterministicPermissionId(string code)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"iam-permission:{code}"));
        return new Guid(hash);
    }

}
