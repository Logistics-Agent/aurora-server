using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Xunit;

namespace RoutePlanningAgent.Tests.Commands;

public class CreateAndDeleteRouteHandlerTests
{
    private static List<RouteStopInputDto> Stops(int count = 2)
    {
        var stops = new List<RouteStopInputDto>();
        for (var i = 1; i <= count; i++)
        {
            stops.Add(new RouteStopInputDto
            {
                Sequence = i,
                StopType = "Delivery",
                LocationName = $"Stop {i}",
                Address = $"Địa chỉ {i}",
                Latitude = 10.7,
                Longitude = 106.6,
                ServiceDurationMinutes = 5
            });
        }
        return stops;
    }

    private static void SeedDefaultRiskPolicy(RoutePlanningDbContext context)
    {
        context.TenantRiskPolicyConfigs.Add(new TenantRiskPolicyConfig
        {
            TenantId = TestDb.TenantId,
            PolicyMode = RiskPolicyMode.UsePlatformDefault,
            ActivePolicyId = RouteRiskPolicyProvider.PlatformDefaultPolicyId,
            ActivePolicyVersion = 1
        });
    }

    [Fact]
    public async Task CreateRoute_HappyPath_GhiRouteVaOutboxCungTransaction()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var handler = new CreateRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        var dto = await handler.Handle(new CreateRouteCommand(
            "Route HN-SG", "Mô tả", "Fixed", 1000m, 20m, 1700m, 2400, Stops(3)), CancellationToken.None);

        Assert.Equal("Draft", dto.Status);
        Assert.Equal(3, dto.Stops.Count);
        Assert.Equal(1700m, dto.EstimatedDistanceKm);

        // Route + Outbox trong CÙNG SaveChanges
        Assert.Equal(1, await context.Routes.CountAsync());
        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(RouteCreatedEvent), outbox.EventType);
        Assert.Contains(TestDb.TenantId.ToString(), outbox.Payload);
    }

    [Fact]
    public async Task CreateRoute_ThieuTenantContext_ForbiddenException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var handler = new CreateRouteHandler(
            context, new FakeCurrentUser(null, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new CreateRouteCommand("R", null, "Fixed", 1m, 1m, 0m, 0, Stops()), CancellationToken.None));
    }

    [Fact]
    public async Task CreateRoute_RouteTypeSai_DomainException_KhongSilentFallback()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var handler = new CreateRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new CreateRouteCommand("R", null, "KhongTonTai", 1m, 1m, 0m, 0, Stops()), CancellationToken.None));

        Assert.Equal(0, await context.Routes.CountAsync());
    }

    [Fact]
    public async Task DeleteRoute_SoftDelete_RouteBienKhoiQuery_OutboxDuocGhi()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        SeedDefaultRiskPolicy(context);
        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());

        var handler = new DeleteRouteHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        var result = await handler.Handle(new DeleteRouteCommand(route.Id), CancellationToken.None);

        Assert.True(result);
        // Global query filter ẩn route đã soft-delete
        Assert.Equal(0, await context.Routes.CountAsync());
        // Vẫn còn trong DB (soft delete, không hard delete)
        Assert.Equal(1, await context.Routes.IgnoreQueryFilters().CountAsync());

        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(RouteDeletedEvent), outbox.EventType);
    }

    [Fact]
    public async Task DeleteRoute_DangActive_ConflictException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        SeedDefaultRiskPolicy(context);
        var route = RouteBuilder.Build(status: RouteStatus.Active);
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());

        var handler = new DeleteRouteHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new DeleteRouteCommand(route.Id), CancellationToken.None));
    }
}
