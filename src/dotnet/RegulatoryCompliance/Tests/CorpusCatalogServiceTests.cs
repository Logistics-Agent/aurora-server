using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class CorpusCatalogServiceTests
{
    [Fact]
    public async Task RegulatoryListReturnsPlatformAndCurrentTenantOnlyWithPagination()
    {
        var databaseName = $"corpus-catalog-{Guid.CreateVersion7()}";
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();

        await using (var writeContext = CreateContext(CurrentUser(tenantId), databaseName))
        {
            writeContext.RegulatoryDocuments.AddRange(
                CreateRegulatoryDocument(null, "platform"),
                CreateRegulatoryDocument(tenantId, "tenant"),
                CreateRegulatoryDocument(otherTenantId, "other"));
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateContext(CurrentUser(tenantId), databaseName);
        var page = await new CorpusCatalogService(readContext)
            .ListRegulatorySourcesAsync(1, 1, cancellationToken: default);

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Contains(page.Items[0].Title, new[] { "Platform platform", "Tenant tenant" });
    }

    [Fact]
    public async Task KnowledgeListFiltersByCategoryAndLatestVersionStatus()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext(CurrentUser(tenantId));
        var sop = KnowledgeDocument.CreateTenant(
            tenantId, KnowledgeCategory.Sop, "SOP", "sop://tenant/sop", "vi", DateTimeOffset.UtcNow);
        var guide = KnowledgeDocument.CreateTenant(
            tenantId, KnowledgeCategory.Guide, "Guide", "sop://tenant/guide", "vi", DateTimeOffset.UtcNow);
        sop.AddVersion("sop-1", "1.0", Sha256, "knowledge/sop.md", "sop.md", "text/markdown", 10, DateTimeOffset.UtcNow);
        guide.AddVersion("guide-1", "1.0", Sha256, "knowledge/guide.md", "guide.md", "text/markdown", 10, DateTimeOffset.UtcNow);
        context.KnowledgeDocuments.AddRange(sop, guide);
        await context.SaveChangesAsync();

        var page = await new CorpusCatalogService(context)
            .ListKnowledgeDocumentsAsync(1, 20, RegulatoryIngestionStatus.Pending, KnowledgeCategory.Sop);

        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal("SOP", item.Title);
        Assert.Equal(RegulatoryIngestionStatus.Pending, item.LatestVersion!.Status);
    }

    private static RegulatoryDocument CreateRegulatoryDocument(Guid? tenantId, string suffix)
    {
        var document = tenantId.HasValue
            ? RegulatoryDocument.CreateTenant(
                tenantId.Value, "Authority", $"{(tenantId == null ? "Platform" : "Tenant")} {suffix}",
                $"https://regulations.example/{suffix}-{tenantId}", "VN", RegulationType.Customs, "vi", DateTimeOffset.UtcNow)
            : RegulatoryDocument.CreatePlatform(
                "Authority", $"Platform {suffix}", $"https://regulations.example/{suffix}",
                "VN", RegulationType.Customs, "vi", DateTimeOffset.UtcNow);
        document.AddVersion(
            $"{suffix}-1", "1.0", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            null, Sha256, $"regulatory/{suffix}.md", $"{suffix}.md", "text/markdown", 10, DateTimeOffset.UtcNow);
        return document;
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

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private const string Sha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
}
