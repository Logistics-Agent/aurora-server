using Microsoft.EntityFrameworkCore;
using Shared.Security;
using Shared.Interceptors;
using MailService.Domain.Entities;

namespace MailService.Infrastructure.Persistence;

public class MailServiceDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public MailServiceDbContext(DbContextOptions<MailServiceDbContext> options, ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
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

        // Apply Global Query Filter for Multi-Tenant Isolation
        Guid currentTenantId = _currentUserService?.TenantId ?? Guid.Empty;

        modelBuilder.Entity<Domain.Entities.Domain>(b =>
        {
            b.ToTable("domains");
            b.HasKey(d => d.Id);
            b.HasIndex(d => d.TenantId);
            b.HasIndex(d => d.DomainName).IsUnique();
            b.HasQueryFilter(d => currentTenantId == Guid.Empty || d.TenantId == currentTenantId);
        });

        modelBuilder.Entity<Mailbox>(b =>
        {
            b.ToTable("mailboxes");
            b.HasKey(m => m.Id);
            b.HasIndex(m => m.TenantId);
            b.HasIndex(m => m.DomainId);
            b.HasIndex(m => m.FullAddress).IsUnique();
            b.HasQueryFilter(m => currentTenantId == Guid.Empty || m.TenantId == currentTenantId);
        });

        modelBuilder.Entity<Alias>(b =>
        {
            b.ToTable("aliases");
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.TenantId);
            b.HasIndex(a => a.AliasAddress).IsUnique();
            b.HasQueryFilter(a => currentTenantId == Guid.Empty || a.TenantId == currentTenantId);
        });

        modelBuilder.Entity<EmailDraft>(b =>
        {
            b.ToTable("email_drafts");
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.DraftRootId, d.RevisionNumber });
            b.HasIndex(d => new { d.MailboxId, d.Status, d.IsLatestRevision });
            b.HasQueryFilter(d => currentTenantId == Guid.Empty || d.TenantId == currentTenantId);
        });

        modelBuilder.Entity<ProcessedMessage>(b =>
        {
            b.ToTable("processed_messages");
            b.HasKey(p => p.Id);
            b.HasIndex(p => new { p.TenantId, p.ReceivedAt });
            b.HasIndex(p => new { p.TenantId, p.MessageId });
            b.HasQueryFilter(p => currentTenantId == Guid.Empty || p.TenantId == currentTenantId);
        });

        modelBuilder.Entity<SecurityCheckResult>(b =>
        {
            b.ToTable("security_check_results");
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.ProcessedMessageId);
            b.HasIndex(s => s.TenantId);
            b.HasQueryFilter(s => currentTenantId == Guid.Empty || s.TenantId == currentTenantId);
        });

        modelBuilder.Entity<QuarantineRecord>(b =>
        {
            b.ToTable("quarantine_records");
            b.HasKey(q => q.Id);
            b.HasIndex(q => new { q.TenantId, q.Status });
            b.HasQueryFilter(q => currentTenantId == Guid.Empty || q.TenantId == currentTenantId);
        });

        modelBuilder.Entity<AuditRecord>(b =>
        {
            b.ToTable("audit_records");
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.TenantId, a.Timestamp });
            b.HasQueryFilter(a => currentTenantId == Guid.Empty || a.TenantId == currentTenantId);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(o => o.Id);
            b.HasIndex(o => o.CreatedAt);
        });
    }
}
