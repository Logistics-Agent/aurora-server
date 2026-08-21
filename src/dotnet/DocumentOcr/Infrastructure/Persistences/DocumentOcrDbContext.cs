using DocumentOcr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Infrastructure.Persistences;

public sealed class DocumentOcrDbContext(
    DbContextOptions<DocumentOcrDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<DocumentOcrJob> Jobs => Set<DocumentOcrJob>();
    public DbSet<OcrProviderAttempt> ProviderAttempts => Set<OcrProviderAttempt>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(_auditInterceptor);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureJob(modelBuilder);
        ConfigureAttempt(modelBuilder);
        ConfigureInbox(modelBuilder);
        ConfigureOutbox(modelBuilder);
    }

    private void ConfigureJob(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentOcrJob>(entity =>
        {
            entity.ToTable("document_ocr_jobs");
            entity.HasKey(job => job.Id);
            entity.HasAlternateKey(job => new { job.TenantId, job.Id });
            entity.HasQueryFilter(job => job.TenantId == _currentUser.TenantId);
            entity.HasIndex(job => new { job.TenantId, job.IdempotencyKey }).IsUnique();
            entity.HasIndex(job => new { job.TenantId, job.ExternalDocumentId });
            entity.HasIndex(job => new { job.TenantId, job.ExternalShipmentId });
            entity.HasIndex(job => new { job.TenantId, job.Status, job.CreatedAt, job.Id });
            entity.HasIndex(job => new { job.Status, job.NextAttemptAt, job.CreatedAt });
            entity.HasIndex(job => new { job.Status, job.LeaseExpiresAt });

            entity.Property(job => job.IdempotencyKey).HasMaxLength(150).IsRequired();
            entity.Property(job => job.StorageReference).HasMaxLength(1_000).IsRequired();
            entity.Property(job => job.FileName).HasMaxLength(255).IsRequired();
            entity.Property(job => job.MimeType).HasMaxLength(150).IsRequired();
            entity.Property(job => job.DocumentTypeHint).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(job => job.DetectedDocumentType).HasConversion<string>().HasMaxLength(50);
            entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(30).IsRequired().IsConcurrencyToken();
            entity.Property(job => job.NormalizedJson).HasColumnType("jsonb");
            entity.Property(job => job.FieldConfidenceJson).HasColumnType("jsonb");
            entity.Property(job => job.Confidence).HasPrecision(5, 4);
            entity.Property(job => job.ErrorCode).HasMaxLength(100);
            entity.Property(job => job.ErrorMessage).HasMaxLength(2_000);
            ConfigureAudit(entity);

            entity.HasMany(job => job.Attempts)
                .WithOne()
                .HasForeignKey(attempt => new { attempt.TenantId, attempt.JobId })
                .HasPrincipalKey(job => new { job.TenantId, job.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(job => job.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private void ConfigureAttempt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OcrProviderAttempt>(entity =>
        {
            entity.ToTable("ocr_provider_attempts");
            entity.HasKey(attempt => attempt.Id);
            entity.HasQueryFilter(attempt => attempt.TenantId == _currentUser.TenantId);
            entity.HasIndex(attempt => new { attempt.TenantId, attempt.JobId, attempt.StartedAt });
            entity.HasIndex(attempt => new { attempt.TenantId, attempt.ProviderRequestId });
            entity.Property(attempt => attempt.ProviderName).HasMaxLength(100).IsRequired();
            entity.Property(attempt => attempt.Outcome).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(attempt => attempt.ProviderRequestId).HasMaxLength(200);
            entity.Property(attempt => attempt.ErrorCode).HasMaxLength(100);
            entity.Property(attempt => attempt.Diagnostics).HasMaxLength(2_000);
            ConfigureAudit(entity);
        });
    }

    private void ConfigureInbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(message => message.Id);
            entity.HasQueryFilter(message => message.TenantId == _currentUser.TenantId);
            entity.HasIndex(message => new { message.SourceEventType, message.SourceEventId }).IsUnique();
            entity.HasIndex(message => new { message.TenantId, message.ReceivedAt });
            entity.Property(message => message.SourceEventType).HasMaxLength(256).IsRequired();
            ConfigureAudit(entity);
        });
    }

    private void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.HasQueryFilter(message => message.TenantId == _currentUser.TenantId);
            entity.HasIndex(message => message.EventId).IsUnique();
            entity.HasIndex(message => new { message.ProcessedAt, message.RetryCount, message.OccurredAt });
            entity.HasIndex(message => new { message.TenantId, message.OccurredAt });
            entity.Property(message => message.EventType).HasMaxLength(256).IsRequired();
            entity.Property(message => message.Content).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.Error).HasMaxLength(2_000);
            ConfigureAudit(entity);
        });
    }

    private static void ConfigureAudit<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : Shared.Entity.TenantAuditableEntity
    {
        entity.Property(item => item.TenantId).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.Property(item => item.CreatedBy).HasMaxLength(100);
        entity.Property(item => item.UpdatedBy).HasMaxLength(100);
    }
}
