using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class RegulatoryCompliancePersistenceModelTests
{
    private static readonly Type[] TenantEntityTypes =
    [
        typeof(ComplianceEvaluation),
        typeof(ComplianceFinding),
        typeof(ComplianceCitation),
        typeof(RetrievalTrace),
        typeof(InboxMessage),
        typeof(OutboxMessage)
    ];

    [Fact]
    public void ModelDefinesVisibilityTenantFiltersAndOperationalIndexes()
    {
        using var context = CreateRelationalContext(new CurrentUserService());

        Assert.NotEmpty(context.Model.FindEntityType(typeof(RegulatoryDocument))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(context.Model.FindEntityType(typeof(RegulatoryDocumentVersion))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(context.Model.FindEntityType(typeof(RegulatoryChunk))!.GetDeclaredQueryFilters());
        Assert.All(TenantEntityTypes, type =>
            Assert.NotEmpty(context.Model.FindEntityType(type)!.GetDeclaredQueryFilters()));

        AssertIndex(context, typeof(RegulatoryDocument), true,
            "ScopeKey", "CanonicalSourceUri", "JurisdictionCode", "LanguageCode");
        AssertIndex(context, typeof(RegulatoryDocumentVersion), true,
            "RegulatoryDocumentId", "VersionLabel");
        AssertIndex(context, typeof(RegulatoryDocumentVersion), true,
            "RegulatoryDocumentId", "ContentSha256");
        AssertIndex(context, typeof(RegulatoryChunk), true,
            "RegulatoryDocumentVersionId", "Sequence");
        AssertIndex(context, typeof(ComplianceEvaluation), true,
            "TenantId", "IdempotencyKey");
        AssertIndex(context, typeof(InboxMessage), true,
            "SourceEventType", "SourceEventId");
        AssertIndex(context, typeof(OutboxMessage), true, "EventId");
    }

    [Fact]
    public void ModelUsesJsonPrecisionAndSafeAggregateDeleteBehavior()
    {
        using var context = CreateRelationalContext(new CurrentUserService());
        var evaluation = context.Model.FindEntityType(typeof(ComplianceEvaluation))!;
        var citation = context.Model.FindEntityType(typeof(ComplianceCitation))!;

        Assert.Equal("jsonb", evaluation.FindProperty(nameof(ComplianceEvaluation.RequestSnapshotJson))!.GetColumnType());
        Assert.Equal("jsonb", evaluation.FindProperty(nameof(ComplianceEvaluation.AssumptionsJson))!.GetColumnType());
        Assert.Equal(5, evaluation.FindProperty(nameof(ComplianceEvaluation.Confidence))!.GetPrecision());
        Assert.Equal(4, evaluation.FindProperty(nameof(ComplianceEvaluation.Confidence))!.GetScale());
        Assert.Contains(citation.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(RegulatoryChunk) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public async Task SourceVisibilityReturnsPlatformAndCurrentTenantOnly()
    {
        var databaseName = $"compliance-visibility-{Guid.CreateVersion7()}";
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        await using (var writeContext = CreateContext(CurrentUser(tenantA), databaseName))
        {
            writeContext.RegulatoryDocuments.Add(CreatePlatformDocument("platform"));
            writeContext.RegulatoryDocuments.Add(CreateTenantDocument(tenantA, "tenant-a"));
            writeContext.RegulatoryDocuments.Add(CreateTenantDocument(tenantB, "tenant-b"));
            await writeContext.SaveChangesAsync();
        }

        await using var tenantContext = CreateContext(CurrentUser(tenantA), databaseName);
        var tenantDocuments = await tenantContext.RegulatoryDocuments
            .OrderBy(document => document.Title)
            .ToListAsync();

        Assert.Equal(2, tenantDocuments.Count);
        Assert.Contains(tenantDocuments, document => document.Visibility == SourceVisibility.Platform);
        Assert.Contains(tenantDocuments, document => document.TenantId == tenantA);
        Assert.DoesNotContain(tenantDocuments, document => document.TenantId == tenantB);

        await using var missingTenantContext = CreateContext(new CurrentUserService(), databaseName);
        var anonymousDocuments = await missingTenantContext.RegulatoryDocuments.ToListAsync();
        Assert.Single(anonymousDocuments);
        Assert.Equal(SourceVisibility.Platform, anonymousDocuments[0].Visibility);
    }

    [Fact]
    public async Task MissingTenantContextNeverExposesEvaluations()
    {
        var databaseName = $"compliance-evaluation-{Guid.CreateVersion7()}";
        var tenantId = Guid.CreateVersion7();
        await using (var writeContext = CreateContext(CurrentUser(tenantId), databaseName))
        {
            writeContext.ComplianceEvaluations.Add(ComplianceEvaluation.Create(
                tenantId,
                "evaluation-001",
                Guid.CreateVersion7(),
                new string('a', 64),
                "{\"cargo\":[]}",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
            await writeContext.SaveChangesAsync();
        }

        await using var missingTenantContext = CreateContext(new CurrentUserService(), databaseName);
        Assert.Empty(await missingTenantContext.ComplianceEvaluations.ToListAsync());
    }

    private static RegulatoryComplianceDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static RegulatoryComplianceDbContext CreateRelationalContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseNpgsql("Host=model-inspection;Database=regulatory_compliance_model", npgsql => npgsql.UseVector())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private static RegulatoryDocument CreatePlatformDocument(string suffix) =>
        RegulatoryDocument.CreatePlatform(
            "Platform Authority",
            $"Platform {suffix}",
            $"https://regulations.example/{suffix}",
            "GLOBAL",
            RegulationType.Customs,
            "en",
            DateTimeOffset.UtcNow);

    private static RegulatoryDocument CreateTenantDocument(Guid tenantId, string suffix) =>
        RegulatoryDocument.CreateTenant(
            tenantId,
            "Tenant Authority",
            $"Tenant {suffix}",
            $"https://regulations.example/{suffix}",
            "VN",
            RegulationType.Customs,
            "en",
            DateTimeOffset.UtcNow);

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
