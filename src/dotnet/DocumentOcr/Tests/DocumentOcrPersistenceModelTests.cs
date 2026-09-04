using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests;

public sealed class DocumentOcrPersistenceModelTests
{
    private static readonly Type[] TenantEntityTypes =
    [
        typeof(DocumentOcrJob), typeof(OcrProviderAttempt), typeof(InboxMessage), typeof(OutboxMessage)
    ];

    [Fact]
    public void ModelDefinesTenantFiltersAndOperationalIndexes()
    {
        using var context = CreateRelationalContext(new CurrentUserService());

        Assert.All(TenantEntityTypes, type =>
            Assert.NotEmpty(context.Model.FindEntityType(type)!.GetDeclaredQueryFilters()));

        AssertIndex(context, typeof(DocumentOcrJob), true, "TenantId", "IdempotencyKey");
        AssertIndex(context, typeof(DocumentOcrJob), false, "Status", "NextAttemptAt", "CreatedAt");
        AssertIndex(context, typeof(InboxMessage), true, "SourceEventType", "SourceEventId");
        AssertIndex(context, typeof(OutboxMessage), true, "EventId");
        AssertIndex(context, typeof(OutboxMessage), false, "ProcessedAt", "RetryCount", "OccurredAt");
    }

    [Fact]
    public void ModelUsesJsonConfidenceAndTenantSafeAggregateRelationship()
    {
        using var context = CreateRelationalContext(new CurrentUserService());
        var job = context.Model.FindEntityType(typeof(DocumentOcrJob))!;
        var attempt = context.Model.FindEntityType(typeof(OcrProviderAttempt))!;
        var foreignKey = attempt.GetForeignKeys().Single();

        Assert.Equal("jsonb", job.FindProperty(nameof(DocumentOcrJob.NormalizedJson))!.GetColumnType());
        Assert.Equal("jsonb", job.FindProperty(nameof(DocumentOcrJob.FieldConfidenceJson))!.GetColumnType());
        Assert.Equal(5, job.FindProperty(nameof(DocumentOcrJob.Confidence))!.GetPrecision());
        Assert.Equal(4, job.FindProperty(nameof(DocumentOcrJob.Confidence))!.GetScale());
        Assert.Equal(["TenantId", "JobId"], foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(["TenantId", "Id"], foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public async Task MissingTenantContextNeverDisablesFilters()
    {
        var databaseName = $"document-ocr-model-{Guid.CreateVersion7()}";
        var tenantId = Guid.CreateVersion7();
        var tenant = new CurrentUserService();
        tenant.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);

        await using (var writeContext = CreateContext(tenant, databaseName))
        {
            writeContext.Jobs.Add(DocumentOcrJob.Create(
                tenantId,
                "request-001",
                "objects/tenant/document.pdf",
                "invoice.pdf",
                "application/pdf",
                1_024,
                OcrDocumentType.CommercialInvoice,
                Guid.CreateVersion7(),
                null,
                DateTimeOffset.UtcNow));
            await writeContext.SaveChangesAsync();
        }

        await using var missingTenantContext = CreateContext(new CurrentUserService(), databaseName);

        Assert.Empty(await missingTenantContext.Jobs.ToListAsync());
    }

    private static DocumentOcrDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new DocumentOcrDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static DocumentOcrDbContext CreateRelationalContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseNpgsql("Host=model-inspection;Database=document_ocr_model")
            .Options;
        return new DocumentOcrDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static void AssertIndex(
        DbContext context,
        Type entityType,
        bool unique,
        params string[] propertyNames)
    {
        var index = context.Model.FindEntityType(entityType)!.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.NotNull(index);
        Assert.Equal(unique, index!.IsUnique);
    }
}
