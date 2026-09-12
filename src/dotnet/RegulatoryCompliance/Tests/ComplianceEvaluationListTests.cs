using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class ComplianceEvaluationListTests
{
    [Fact]
    public async Task List_returns_requested_page_in_descending_time_order_for_current_tenant()
    {
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, 1, "STAFF", []);
        await using var context = CreateContext(currentUser);

        context.ComplianceEvaluations.AddRange(
            CreateEvaluation(tenantId, "old", DateTimeOffset.Parse("2026-09-10T00:00:00Z")),
            CreateEvaluation(tenantId, "new", DateTimeOffset.Parse("2026-09-12T00:00:00Z")),
            CreateEvaluation(tenantId, "middle", DateTimeOffset.Parse("2026-09-11T00:00:00Z")),
            CreateEvaluation(otherTenantId, "other-tenant", DateTimeOffset.Parse("2026-09-13T00:00:00Z")));
        await context.SaveChangesAsync();

        var service = new ComplianceEvaluationService(
            context,
            new NoopRetrievalService(),
            currentUser,
            TimeProvider.System);

        var page = await service.ListAsync(2, 1);

        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(3, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("middle", page.Items.Single().IdempotencyKey);
    }

    [Fact]
    public async Task List_filters_by_shipment_without_cross_tenant_leak()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, 1, "STAFF", []);
        await using var context = CreateContext(currentUser);
        context.ComplianceEvaluations.AddRange(
            CreateEvaluation(tenantId, "match", DateTimeOffset.UtcNow, shipmentId),
            CreateEvaluation(tenantId, "different", DateTimeOffset.UtcNow, Guid.CreateVersion7()),
            CreateEvaluation(Guid.CreateVersion7(), "other-tenant", DateTimeOffset.UtcNow, shipmentId));
        await context.SaveChangesAsync();

        var service = new ComplianceEvaluationService(
            context,
            new NoopRetrievalService(),
            currentUser,
            TimeProvider.System);

        var page = await service.ListAsync(1, 20, shipmentId);

        Assert.Equal(1, page.TotalItems);
        Assert.Equal("match", page.Items.Single().IdempotencyKey);
    }

    private static ComplianceEvaluation CreateEvaluation(
        Guid tenantId,
        string key,
        DateTimeOffset requestedAt,
        Guid? shipmentId = null) =>
        ComplianceEvaluation.Create(
            tenantId,
            key,
            shipmentId ?? Guid.CreateVersion7(),
            new string('a', 64),
            "{}",
            requestedAt,
            requestedAt);

    private static RegulatoryComplianceDbContext CreateContext(CurrentUserService currentUser) =>
        new(
            new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
                .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
                .Options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));

    private sealed class NoopRetrievalService : IRegulationRetrievalService
    {
        public Task<RegulationQueryResult> QueryAsync(
            RegulationQueryInput input,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
