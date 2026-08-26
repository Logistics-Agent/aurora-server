using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using RoutePlanningAgent.Domain;
using Route = RoutePlanningAgent.Domain.Route; // tránh nhầm với Microsoft.AspNetCore.Routing.Route

namespace RoutePlanningAgent.Infrastructure.Persistences;

public class RoutePlanningDbContext(
    DbContextOptions<RoutePlanningDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly Guid? _tenantId = currentUser.TenantId;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<RouteOptimizationHistory> OptimizationHistories => Set<RouteOptimizationHistory>();
    public DbSet<TenantRiskPolicyConfig> TenantRiskPolicyConfigs => Set<TenantRiskPolicyConfig>();
    public DbSet<TenantRiskPolicy> TenantRiskPolicies => Set<TenantRiskPolicy>();
    public DbSet<TenantRiskRule> TenantRiskRules => Set<TenantRiskRule>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RouteDecisionAuditLog> DecisionAuditLogs => Set<RouteDecisionAuditLog>();
    public DbSet<TenantRuleConfig> TenantRuleConfigs => Set<TenantRuleConfig>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filters (Tenant Isolation Fail-Closed + IsDeleted)
        // Lưu ý: enum lưu dạng int (ordinal) theo quyết định thiết kế — chỉ append member, không reorder.
        modelBuilder.Entity<Route>(e =>
        {
            e.HasQueryFilter(r =>
                _tenantId.HasValue && r.TenantId == _tenantId.Value
                && !r.IsDeleted);

            e.HasIndex(r => r.TenantId);
            e.HasIndex(r => new { r.TenantId, r.Status });

            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(1000);
            e.Property(r => r.EstimatedDistanceKm).HasPrecision(12, 3);
            e.Property(r => r.MaxWeightKg).HasPrecision(12, 3);
            e.Property(r => r.MaxVolumeM3).HasPrecision(12, 3);
        });

        modelBuilder.Entity<RouteStop>(e =>
        {
            e.HasIndex(s => s.RouteId);
            e.HasIndex(s => new { s.RouteId, s.Sequence });

            e.Property(s => s.LocationName).HasMaxLength(200).IsRequired();
            e.Property(s => s.Address).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<RouteOptimizationHistory>(e =>
        {
            e.HasIndex(h => h.RouteId);

            e.Property(h => h.Provider).HasMaxLength(50).IsRequired();
            e.Property(h => h.Model).HasMaxLength(100).IsRequired();
            e.Property(h => h.PromptVersion).HasMaxLength(20).IsRequired();
            e.Property(h => h.TotalDistanceKm).HasPrecision(12, 3);
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.HasQueryFilter(a =>
                _tenantId.HasValue && a.TenantId == _tenantId.Value);

            e.HasIndex(a => a.RouteId);
            e.HasIndex(a => new { a.RouteId, a.RouteVersion, a.PolicyVersion });

            e.Property(a => a.PolicyId).HasMaxLength(100).IsRequired();
            e.Property(a => a.Feature).HasMaxLength(100).IsRequired();
            e.Property(a => a.Reason).HasMaxLength(2000).IsRequired();
            e.Property(a => a.ReviewerComment).HasMaxLength(1000);
            e.Property(a => a.RejectionReason).HasMaxLength(1000);
        });

        modelBuilder.Entity<RiskAssessment>(e =>
        {
            e.HasQueryFilter(a =>
                _tenantId.HasValue && a.TenantId == _tenantId.Value);

            e.HasIndex(a => a.RouteId);
            e.HasIndex(a => new { a.RouteId, a.RouteVersion });
            e.HasIndex(a => new { a.RouteId, a.RouteVersion, a.PolicyVersion });

            e.Property(a => a.PolicyId).HasMaxLength(100).IsRequired();
            e.Property(a => a.MatchedRuleCodes).HasMaxLength(1000).IsRequired();
            e.Property(a => a.Source).HasMaxLength(100).IsRequired();
            e.Property(a => a.ReasonCodes).HasMaxLength(500).IsRequired();
            e.Property(a => a.ReasonDetails).HasMaxLength(2000).IsRequired();
            e.Property(a => a.PolicyApplied).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<RouteDecisionAuditLog>(e =>
        {
            e.HasQueryFilter(a =>
                _tenantId.HasValue && a.TenantId == _tenantId.Value);


            e.HasIndex(a => a.RouteId);

            e.Property(a => a.RiskLevel).HasMaxLength(50).IsRequired();
            e.Property(a => a.AutomationDecision).HasMaxLength(100).IsRequired();
            e.Property(a => a.LlmProvider).HasMaxLength(50);
            e.Property(a => a.LlmModel).HasMaxLength(100);
        });

        modelBuilder.Entity<TenantRuleConfig>(e =>
        {
            e.HasIndex(c => new { c.TenantId, c.RuleName }).IsUnique();

            e.Property(c => c.RuleName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<TenantRiskPolicyConfig>(e =>
        {
            e.HasIndex(c => c.TenantId).IsUnique();

            e.Property(c => c.ActivePolicyId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<TenantRiskPolicy>(e =>
        {
            e.HasQueryFilter(p =>
                _tenantId.HasValue && p.TenantId == _tenantId.Value
                && !p.IsDeleted);

            e.HasIndex(p => p.TenantId);
            e.HasIndex(p => new { p.TenantId, p.Scope, p.Version });
            e.HasIndex(p => new { p.TenantId, p.Scope, p.Status });

            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.Scope).HasMaxLength(100).IsRequired();
            e.Property(p => p.SourceDocumentId).HasMaxLength(200);
            e.Property(p => p.ReviewerComment).HasMaxLength(1000);
            e.Property(p => p.RejectionReason).HasMaxLength(1000);

            e.HasMany(p => p.Rules)
                .WithOne(r => r.Policy)
                .HasForeignKey(r => r.PolicyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantRiskRule>(e =>
        {
            e.HasQueryFilter(r =>
                _tenantId.HasValue && r.TenantId == _tenantId.Value
                && !r.IsDeleted);

            e.HasIndex(r => r.PolicyId);
            e.HasIndex(r => new { r.PolicyId, r.RuleCode });

            e.Property(r => r.RuleCode).HasMaxLength(100).IsRequired();
            e.Property(r => r.RuleName).HasMaxLength(100).IsRequired();
            e.Property(r => r.SourceReference).HasMaxLength(500);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasIndex(m => new { m.ProcessedAt, m.CreatedAt });

            e.Property(m => m.EventType).HasMaxLength(256).IsRequired();
            e.Property(m => m.Payload).IsRequired();
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
