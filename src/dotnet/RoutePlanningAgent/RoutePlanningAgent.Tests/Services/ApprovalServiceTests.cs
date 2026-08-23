using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Exceptions;
using Xunit;

namespace RoutePlanningAgent.Tests.Services;

public class ApprovalServiceTests
{
    [Fact]
    public async Task Create_KhongTuSaveChanges_CallerGiuTransaction()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var service = new ApprovalService(context);
        var approval = await service.CreateAsync(route.Id, "Lý do", "AI summary", null);

        // Chưa persist — caller phải SaveChanges
        Assert.Equal(0, await context.ApprovalRequests.AsNoTracking().CountAsync());

        await context.SaveChangesAsync();
        Assert.Equal(1, await context.ApprovalRequests.AsNoTracking().CountAsync());
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
    }

    [Fact]
    public async Task Approve_RouteChuyenSangReady()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var service = new ApprovalService(context);
        var approval = await service.CreateAsync(route.Id, "Lý do", "AI summary", null);
        await context.SaveChangesAsync();

        var result = await service.ApproveAsync(approval.Id, TestDb.UserId, "OK");

        Assert.Equal(ApprovalStatus.Approved, result.Status);
        Assert.Equal(TestDb.UserId, result.ReviewedByUserId);
        Assert.Equal(RouteStatus.Ready, route.Status);
    }

    [Fact]
    public async Task Reject_BatBuocReason_RouteChuyenSangCancelled()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var service = new ApprovalService(context);
        var approval = await service.CreateAsync(route.Id, "Lý do", "AI summary", null);
        await context.SaveChangesAsync();

        // Reason rỗng → DomainException
        await Assert.ThrowsAsync<DomainException>(
            () => service.RejectAsync(approval.Id, TestDb.UserId, "  ", null));

        var result = await service.RejectAsync(approval.Id, TestDb.UserId, "Quá tải trọng cho phép", "note");

        Assert.Equal(ApprovalStatus.Rejected, result.Status);
        Assert.Equal("Quá tải trọng cho phép", result.RejectionReason);
        Assert.Equal(RouteStatus.Cancelled, route.Status);
    }

    [Fact]
    public async Task Approve_DaXuLyRoi_ConflictException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var service = new ApprovalService(context);
        var approval = await service.CreateAsync(route.Id, "Lý do", "AI summary", null);
        await context.SaveChangesAsync();

        await service.ApproveAsync(approval.Id, TestDb.UserId, null);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.ApproveAsync(approval.Id, TestDb.UserId, null));
        await Assert.ThrowsAsync<ConflictException>(
            () => service.RejectAsync(approval.Id, TestDb.UserId, "reason", null));
    }

    [Fact]
    public async Task Approve_KhongTonTai_NotFoundException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var service = new ApprovalService(context);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ApproveAsync(Guid.NewGuid(), TestDb.UserId, null));
    }
}
