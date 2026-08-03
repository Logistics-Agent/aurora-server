using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Events;
using Shared.Exceptions;
using Xunit;
using Route = RoutePlanningAgent.Domain.Route;

namespace RoutePlanningAgent.Tests.Commands;

public class OptimizeRouteCommandTests
{
    [Fact]
    public async Task Optimize_ReorderStops_CapNhatEtaTotalsVersionStatus()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build(stopCount: 3);
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var stops = route.Stops.OrderBy(s => s.Sequence).ToList();

        // Solver đảo thứ tự stop 2 và 3
        var solver = Substitute.For<IRouteOptimizationService>();
        solver.OptimizeAsync(Arg.Any<Route>(), Arg.Any<CancellationToken>())
            .Returns(new RouteOptimizationResult
            {
                Stops =
                [
                    new OptimizedStop(stops[0].Id, 1, 0),
                    new OptimizedStop(stops[2].Id, 2, 25),
                    new OptimizedStop(stops[1].Id, 3, 55)
                ],
                TotalDistanceKm = 42.5m,
                TotalDurationMinutes = 65
            });

        var handler = new OptimizeRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), solver, new OutboxWriter(context));

        var dto = await handler.Handle(new OptimizeRouteCommand(route.Id), CancellationToken.None);

        Assert.Equal("Ready", dto.Status);
        Assert.Equal(42.5m, dto.EstimatedDistanceKm);
        Assert.Equal(65, dto.EstimatedDurationMinutes);
        Assert.NotNull(dto.OptimizedAt);
        Assert.Equal(2, dto.Version); // 1 → 2

        // Stop cũ thứ 3 giờ đứng Sequence 2 với ETA 25 phút
        var reordered = dto.Stops.Single(s => s.Id == stops[2].Id);
        Assert.Equal(2, reordered.Sequence);
        Assert.Equal(25, reordered.EstimatedArrivalMinutes);

        // History + outbox event trong cùng transaction
        Assert.Equal(1, await context.OptimizationHistories.CountAsync());
        var history = await context.OptimizationHistories.SingleAsync();
        Assert.Equal("VROOM", history.Provider);
        Assert.Equal("OSRM-MLD", history.Model);

        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(RouteOptimizedEvent), outbox.EventType);
    }

    [Fact]
    public async Task Optimize_RouteDangActive_ConflictException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build(status: RouteStatus.Active);
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var handler = new OptimizeRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId),
            Substitute.For<IRouteOptimizationService>(), new OutboxWriter(context));

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new OptimizeRouteCommand(route.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Optimize_RouteKhongTonTai_NotFoundException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var handler = new OptimizeRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId),
            Substitute.For<IRouteOptimizationService>(), new OutboxWriter(context));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new OptimizeRouteCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
