using Microsoft.EntityFrameworkCore;
using Shared.Security;
using Shared.Interceptors;
using MailService.Domain.Entities;

namespace MailService.Infrastructure.Persistence;

public class MailServiceDbContext : DbContext
{
    private readonly Guid? _tenantId;

    public MailServiceDbContext(
        DbContextOptions<MailServiceDbContext> options,
        ICurrentUserService currentUser)
        : base(options)
    {
        _tenantId = currentUser.TenantId;
    }

    public DbSet<Domain.Entities.Domain> Domains => Set<Domain.Entities.Domain>();
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<Alias> Aliases => Set<Alias>();
    public DbSet<EmailDraft> EmailDrafts => Set<EmailDraft>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<SecurityCheckResult> SecurityCheckResults => Set<SecurityCheckResult>();
    public DbSet<QuarantineRecord> QuarantineRecords => Set<QuarantineRecord>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Global Query Filter for Multi-Tenant Isolation (Fail-Closed)
        modelBuilder.Entity<Domain.Entities.Domain>(b =>
        {
            b.ToTable("domains");
            b.HasKey(d => d.Id);
            b.HasIndex(d => d.TenantId);
            b.HasIndex(d => d.DomainName).IsUnique();
            b.HasQueryFilter(d => _tenantId.HasValue && d.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<Mailbox>(b =>
        {
            b.ToTable("mailboxes");
            b.HasKey(m => m.Id);
            b.HasIndex(m => m.TenantId);
            b.HasIndex(m => m.DomainId);
            b.HasIndex(m => m.FullAddress).IsUnique();
            b.HasQueryFilter(m => _tenantId.HasValue && m.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<Alias>(b =>
        {
            b.ToTable("aliases");
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.TenantId);
            b.HasIndex(a => a.AliasAddress).IsUnique();
            b.HasQueryFilter(a => _tenantId.HasValue && a.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<EmailDraft>(b =>
        {
            b.ToTable("email_drafts");
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.DraftRootId, d.RevisionNumber });
            b.HasIndex(d => new { d.MailboxId, d.Status, d.IsLatestRevision });
            b.HasQueryFilter(d => _tenantId.HasValue && d.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<ProcessedMessage>(b =>
        {
            b.ToTable("processed_messages");
            b.HasKey(p => p.Id);
            b.HasIndex(p => new { p.TenantId, p.ReceivedAt });
            b.HasIndex(p => new { p.TenantId, p.MessageId });
            b.HasQueryFilter(p => _tenantId.HasValue && p.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<SecurityCheckResult>(b =>
        {
            b.ToTable("security_check_results");
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.ProcessedMessageId);
            b.HasIndex(s => s.TenantId);
            b.HasQueryFilter(s => _tenantId.HasValue && s.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<QuarantineRecord>(b =>
        {
            b.ToTable("quarantine_records");
            b.HasKey(q => q.Id);
            b.HasIndex(q => new { q.TenantId, q.Status });
            b.HasQueryFilter(q => _tenantId.HasValue && q.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<AuditRecord>(b =>
        {
            b.ToTable("audit_records");
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.TenantId, a.Timestamp });
            b.HasQueryFilter(a => _tenantId.HasValue && a.TenantId == _tenantId.Value);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(o => o.Id);
            b.HasIndex(o => o.CreatedAt);
        });
    }
}

