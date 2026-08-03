using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Events;
using Shared.Exceptions;
using Xunit;

namespace RoutePlanningAgent.Tests.Commands;

public class UpdateRouteStatusCommandTests
{
    [Theory]
    // Transition hợp lệ
    [InlineData(RouteStatus.Draft, RouteStatus.Optimizing, true)]
    [InlineData(RouteStatus.Draft, RouteStatus.Cancelled, true)]
    [InlineData(RouteStatus.Optimizing, RouteStatus.Ready, true)]
    [InlineData(RouteStatus.Optimizing, RouteStatus.Draft, true)]
    [InlineData(RouteStatus.Ready, RouteStatus.Active, true)]
    [InlineData(RouteStatus.Ready, RouteStatus.Draft, true)]
    [InlineData(RouteStatus.Ready, RouteStatus.Cancelled, true)]
    [InlineData(RouteStatus.Active, RouteStatus.Completed, true)]
    [InlineData(RouteStatus.Active, RouteStatus.Cancelled, true)]
    [InlineData(RouteStatus.Completed, RouteStatus.Archived, true)]
    [InlineData(RouteStatus.Cancelled, RouteStatus.Archived, true)]
    // Transition KHÔNG hợp lệ
    [InlineData(RouteStatus.Draft, RouteStatus.Active, false)]
    [InlineData(RouteStatus.Draft, RouteStatus.Completed, false)]
    [InlineData(RouteStatus.Active, RouteStatus.Draft, false)]
    [InlineData(RouteStatus.Completed, RouteStatus.Active, false)]
    [InlineData(RouteStatus.Archived, RouteStatus.Draft, false)]
    public async Task TransitionMatrix(RouteStatus from, RouteStatus to, bool allowed)
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build(status: from);
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var handler = new UpdateRouteStatusHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));
        var command = new UpdateRouteStatusCommand(route.Id, to.ToString());

        if (allowed)
        {
            var dto = await handler.Handle(command, CancellationToken.None);
            Assert.Equal(to.ToString(), dto.Status);

            // Outbox event được ghi cùng transaction
            var outbox = await context.OutboxMessages.SingleAsync();
            Assert.Equal(nameof(RouteStatusChangedEvent), outbox.EventType);
        }
        else
        {
            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        }
    }

    [Fact]
    public async Task StatusKhongHopLe_DomainException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var handler = new UpdateRouteStatusHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new UpdateRouteStatusCommand(route.Id, "KhongTonTai"), CancellationToken.None));
    }

    [Fact]
    public async Task RouteKhongTonTai_NotFoundException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var handler = new UpdateRouteStatusHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new UpdateRouteStatusCommand(Guid.NewGuid(), "Optimizing"), CancellationToken.None));
    }
}
