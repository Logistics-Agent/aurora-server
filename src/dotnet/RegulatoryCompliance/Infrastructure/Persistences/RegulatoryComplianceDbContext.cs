using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Infrastructure.Persistences;

public sealed class RegulatoryComplianceDbContext(
    DbContextOptions<RegulatoryComplianceDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<RegulatoryDocument> RegulatoryDocuments => Set<RegulatoryDocument>();
    public DbSet<RegulatoryDocumentVersion> RegulatoryDocumentVersions => Set<RegulatoryDocumentVersion>();
    public DbSet<RegulatoryChunk> RegulatoryChunks => Set<RegulatoryChunk>();
    public DbSet<ComplianceEvaluation> ComplianceEvaluations => Set<ComplianceEvaluation>();
    public DbSet<ComplianceFinding> ComplianceFindings => Set<ComplianceFinding>();
    public DbSet<ComplianceCitation> ComplianceCitations => Set<ComplianceCitation>();
    public DbSet<RetrievalTrace> RetrievalTraces => Set<RetrievalTrace>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(_auditInterceptor);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureRegulatoryDocument(modelBuilder);
        ConfigureRegulatoryDocumentVersion(modelBuilder);
        ConfigureRegulatoryChunk(modelBuilder);
        ConfigureEvaluation(modelBuilder);
        ConfigureFinding(modelBuilder);
        ConfigureCitation(modelBuilder);
        ConfigureRetrievalTrace(modelBuilder);
        ConfigureInbox(modelBuilder);
        ConfigureOutbox(modelBuilder);
    }

    private void ConfigureRegulatoryDocument(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegulatoryDocument>(entity =>
        {
            entity.ToTable("regulatory_documents");
            entity.HasKey(document => document.Id);
            entity.HasQueryFilter(document =>
                document.Visibility == SourceVisibility.Platform ||
                (_currentUser.TenantId.HasValue && document.TenantId == _currentUser.TenantId));
            entity.HasIndex(document => new
            {
                document.ScopeKey,
                document.CanonicalSourceUri,
                document.JurisdictionCode,
                document.LanguageCode
            }).IsUnique();
            entity.HasIndex(document => new
            {
                document.ScopeKey,
                document.JurisdictionCode,
                document.RegulationType,
                document.LanguageCode
            });

            entity.Property(document => document.ScopeKey).IsRequired();
            entity.Property(document => document.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(document => document.Authority).HasMaxLength(200).IsRequired();
            entity.Property(document => document.Title).HasMaxLength(500).IsRequired();
            entity.Property(document => document.CanonicalSourceUri).HasMaxLength(1_000).IsRequired();
            entity.Property(document => document.JurisdictionCode).HasMaxLength(30).IsRequired();
            entity.Property(document => document.RegulationType).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(document => document.LanguageCode).HasMaxLength(15).IsRequired();
            ConfigureAudit(entity);

            entity.HasMany(document => document.Versions)
                .WithOne()
                .HasForeignKey(version => version.RegulatoryDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(document => document.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private void ConfigureRegulatoryDocumentVersion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegulatoryDocumentVersion>(entity =>
        {
            entity.ToTable("regulatory_document_versions");
            entity.HasKey(version => version.Id);
            entity.HasQueryFilter(version =>
                version.Visibility == SourceVisibility.Platform ||
                (_currentUser.TenantId.HasValue && version.TenantId == _currentUser.TenantId));
            entity.HasIndex(version => new { version.RegulatoryDocumentId, version.VersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.RegulatoryDocumentId, version.ContentSha256 }).IsUnique();
            entity.HasIndex(version => new
            {
                version.ScopeKey,
                version.IngestionStatus,
                version.EffectiveFrom,
                version.EffectiveTo
            });
            entity.HasIndex(version => version.SupersedesVersionId);

            entity.Property(version => version.ScopeKey).IsRequired();
            entity.Property(version => version.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(100).IsRequired();
            entity.Property(version => version.ContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(version => version.ContentReference).HasMaxLength(1_000).IsRequired();
            entity.Property(version => version.FileName).HasMaxLength(255).IsRequired();
            entity.Property(version => version.MimeType).HasMaxLength(150).IsRequired();
            entity.Property(version => version.IngestionStatus)
                .HasConversion<string>().HasMaxLength(30).IsRequired().IsConcurrencyToken();
            entity.Property(version => version.ErrorCode).HasMaxLength(100);
            entity.Property(version => version.ErrorMessage).HasMaxLength(2_000);
            ConfigureAudit(entity);

            entity.HasOne<RegulatoryDocumentVersion>()
                .WithMany()
                .HasForeignKey(version => version.SupersedesVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(version => version.Chunks)
                .WithOne()
                .HasForeignKey(chunk => chunk.RegulatoryDocumentVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(version => version.Chunks).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private void ConfigureRegulatoryChunk(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegulatoryChunk>(entity =>
        {
            entity.ToTable("regulatory_chunks");
            entity.HasKey(chunk => chunk.Id);
            entity.HasQueryFilter(chunk =>
                chunk.Visibility == SourceVisibility.Platform ||
                (_currentUser.TenantId.HasValue && chunk.TenantId == _currentUser.TenantId));
            entity.HasIndex(chunk => new { chunk.RegulatoryDocumentVersionId, chunk.Sequence }).IsUnique();
            entity.HasIndex(chunk => new { chunk.ScopeKey, chunk.ContentSha256 });

            entity.Property(chunk => chunk.ScopeKey).IsRequired();
            entity.Property(chunk => chunk.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(chunk => chunk.SectionLabel).HasMaxLength(200);
            entity.Property(chunk => chunk.PageLabel).HasMaxLength(50);
            entity.Property(chunk => chunk.NormalizedText).HasMaxLength(20_000).IsRequired();
            entity.Property(chunk => chunk.ContentSha256).HasMaxLength(64).IsRequired();
            ConfigureAudit(entity);
        });
    }

    private void ConfigureEvaluation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceEvaluation>(entity =>
        {
            entity.ToTable("compliance_evaluations");
            entity.HasKey(evaluation => evaluation.Id);
            entity.HasAlternateKey(evaluation => new { evaluation.TenantId, evaluation.Id });
            entity.HasQueryFilter(evaluation => evaluation.TenantId == _currentUser.TenantId);
            entity.HasIndex(evaluation => new { evaluation.TenantId, evaluation.IdempotencyKey }).IsUnique();
            entity.HasIndex(evaluation => new { evaluation.TenantId, evaluation.ExternalShipmentId, evaluation.RequestedAt });
            entity.HasIndex(evaluation => new { evaluation.TenantId, evaluation.Status, evaluation.RequestedAt });
            entity.HasIndex(evaluation => new { evaluation.TenantId, evaluation.RequestHash });

            entity.Property(evaluation => evaluation.IdempotencyKey).HasMaxLength(150).IsRequired();
            entity.Property(evaluation => evaluation.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(evaluation => evaluation.RequestSnapshotJson).HasColumnType("jsonb").IsRequired();
            entity.Property(evaluation => evaluation.Status)
                .HasConversion<string>().HasMaxLength(30).IsRequired().IsConcurrencyToken();
            entity.Property(evaluation => evaluation.RiskLevel).HasConversion<string>().HasMaxLength(30);
            entity.Property(evaluation => evaluation.EvidenceSufficiency).HasConversion<string>().HasMaxLength(30);
            entity.Property(evaluation => evaluation.Confidence).HasPrecision(5, 4);
            entity.Property(evaluation => evaluation.AssumptionsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(evaluation => evaluation.MissingDocumentsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(evaluation => evaluation.ErrorCode).HasMaxLength(100);
            entity.Property(evaluation => evaluation.ErrorMessage).HasMaxLength(2_000);
            ConfigureTenantAudit(entity);

            entity.HasMany(evaluation => evaluation.Findings)
                .WithOne()
                .HasForeignKey(finding => new { finding.TenantId, finding.ComplianceEvaluationId })
                .HasPrincipalKey(evaluation => new { evaluation.TenantId, evaluation.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(evaluation => evaluation.RetrievalTraces)
                .WithOne()
                .HasForeignKey(trace => new { trace.TenantId, trace.ComplianceEvaluationId })
                .HasPrincipalKey(evaluation => new { evaluation.TenantId, Id = (Guid?)evaluation.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(evaluation => evaluation.Findings).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(evaluation => evaluation.RetrievalTraces).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private void ConfigureFinding(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceFinding>(entity =>
        {
            entity.ToTable("compliance_findings");
            entity.HasKey(finding => finding.Id);
            entity.HasAlternateKey(finding => new { finding.TenantId, finding.Id });
            entity.HasQueryFilter(finding => finding.TenantId == _currentUser.TenantId);
            entity.HasIndex(finding => new { finding.TenantId, finding.ComplianceEvaluationId, finding.Type });
            entity.Property(finding => finding.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(finding => finding.Code).HasMaxLength(100).IsRequired();
            entity.Property(finding => finding.Category).HasMaxLength(100).IsRequired();
            entity.Property(finding => finding.Title).HasMaxLength(300).IsRequired();
            entity.Property(finding => finding.Description).HasMaxLength(4_000).IsRequired();
            entity.Property(finding => finding.Severity).HasConversion<string>().HasMaxLength(30).IsRequired();
            ConfigureTenantAudit(entity);

            entity.HasMany(finding => finding.Citations)
                .WithOne()
                .HasForeignKey(citation => new { citation.TenantId, citation.ComplianceFindingId })
                .HasPrincipalKey(finding => new { finding.TenantId, finding.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(finding => finding.Citations).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private void ConfigureCitation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceCitation>(entity =>
        {
            entity.ToTable("compliance_citations");
            entity.HasKey(citation => citation.Id);
            entity.HasQueryFilter(citation => citation.TenantId == _currentUser.TenantId);
            entity.HasIndex(citation => new
            {
                citation.TenantId,
                citation.ComplianceFindingId,
                citation.RegulatoryChunkId
            }).IsUnique();
            entity.HasIndex(citation => citation.RegulatoryDocumentVersionId);
            entity.Property(citation => citation.Authority).HasMaxLength(200).IsRequired();
            entity.Property(citation => citation.Title).HasMaxLength(500).IsRequired();
            entity.Property(citation => citation.CanonicalSourceUri).HasMaxLength(1_000).IsRequired();
            entity.Property(citation => citation.VersionLabel).HasMaxLength(100).IsRequired();
            entity.Property(citation => citation.SectionLabel).HasMaxLength(200);
            entity.Property(citation => citation.PageLabel).HasMaxLength(50);
            entity.Property(citation => citation.Excerpt).HasMaxLength(4_000).IsRequired();
            entity.Property(citation => citation.RelevanceScore).HasPrecision(5, 4).IsRequired();
            ConfigureTenantAudit(entity);

            entity.HasOne<RegulatoryDocument>()
                .WithMany()
                .HasForeignKey(citation => citation.RegulatoryDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RegulatoryDocumentVersion>()
                .WithMany()
                .HasForeignKey(citation => citation.RegulatoryDocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RegulatoryChunk>()
                .WithMany()
                .HasForeignKey(citation => citation.RegulatoryChunkId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ConfigureRetrievalTrace(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RetrievalTrace>(entity =>
        {
            entity.ToTable("retrieval_traces");
            entity.HasKey(trace => trace.Id);
            entity.HasQueryFilter(trace => trace.TenantId == _currentUser.TenantId);
            entity.HasIndex(trace => new { trace.TenantId, trace.QueryHash, trace.CreatedAt });
            entity.HasIndex(trace => new { trace.TenantId, trace.ComplianceEvaluationId, trace.CreatedAt });
            entity.Property(trace => trace.QueryHash).HasMaxLength(64).IsRequired();
            entity.Property(trace => trace.JurisdictionCode).HasMaxLength(30).IsRequired();
            entity.Property(trace => trace.LanguageCode).HasMaxLength(15).IsRequired();
            entity.Property(trace => trace.RegulationTypesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(trace => trace.EmbeddingModel).HasMaxLength(200).IsRequired();
            entity.Property(trace => trace.MinimumRelevanceScore).HasPrecision(5, 4);
            entity.Property(trace => trace.RetrievedChunkIdsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(trace => trace.ScoresJson).HasColumnType("jsonb").IsRequired();
            entity.Property(trace => trace.EvidenceSufficiency)
                .HasConversion<string>().HasMaxLength(30).IsRequired();
            ConfigureTenantAudit(entity);
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
            ConfigureTenantAudit(entity);
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
            ConfigureTenantAudit(entity);
        });
    }

    private static void ConfigureTenantAudit<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : TenantAuditableEntity
    {
        entity.Property(item => item.TenantId).IsRequired();
        ConfigureAudit(entity);
    }

    private static void ConfigureAudit<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : AuditableEntity
    {
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.Property(item => item.CreatedBy).HasMaxLength(100);
        entity.Property(item => item.UpdatedBy).HasMaxLength(100);
    }
}
