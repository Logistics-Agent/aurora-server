using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using RoutePlanningAgent.Domain;

namespace RoutePlanningAgent.Infrastructure.Persistences;

public class RoutePlanningDbContext(
    DbContextOptions<RoutePlanningDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<RouteOptimizationHistory> OptimizationHistories => Set<RouteOptimizationHistory>();
    public DbSet<TenantAiConfig> TenantAiConfigs => Set<TenantAiConfig>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<RouteDecisionAuditLog> DecisionAuditLogs => Set<RouteDecisionAuditLog>();
    public DbSet<TenantRuleConfig> TenantRuleConfigs => Set<TenantRuleConfig>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filters (Tenant Isolation + IsDeleted)
        modelBuilder.Entity<Route>(e =>
        {
            e.HasQueryFilter(r =>
                (!_currentUser.TenantId.HasValue || r.TenantId == _currentUser.TenantId)
                && !r.IsDeleted);

            e.HasIndex(r => r.TenantId);
            e.HasIndex(r => new { r.TenantId, r.Status });
        });

        modelBuilder.Entity<RouteStop>(e =>
        {
            e.HasIndex(s => s.RouteId);
        });

        modelBuilder.Entity<RouteOptimizationHistory>(e =>
        {
            e.HasIndex(h => h.RouteId);
        });

        modelBuilder.Entity<TenantAiConfig>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.Feature }).IsUnique();
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.HasQueryFilter(a =>
                !_currentUser.TenantId.HasValue || a.TenantId == _currentUser.TenantId);

            e.HasIndex(a => a.RouteId);
        });

        modelBuilder.Entity<RouteDecisionAuditLog>(e =>
        {
            e.HasQueryFilter(a =>
                !_currentUser.TenantId.HasValue || a.TenantId == _currentUser.TenantId);

            e.HasIndex(a => a.RouteId);
        });

        modelBuilder.Entity<TenantRuleConfig>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.RuleName }).IsUnique();
        });

        // Cascade delete RouteStops when Route is deleted
        modelBuilder.Entity<Route>()
            .HasMany(r => r.Stops)
            .WithOne(s => s.Route)
            .HasForeignKey(s => s.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict delete OptimizationHistories
        modelBuilder.Entity<Route>()
            .HasMany(r => r.OptimizationHistories)
            .WithOne()
            .HasForeignKey(h => h.RouteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
