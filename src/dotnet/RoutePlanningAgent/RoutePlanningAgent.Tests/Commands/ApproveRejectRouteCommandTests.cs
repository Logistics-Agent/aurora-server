using NSubstitute;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Exceptions;
using Xunit;

namespace RoutePlanningAgent.Tests.Commands;

public class ApproveRejectRouteCommandTests
{
    private static ApprovalRequest FakeApproval() => new()
    {
        RouteId = Guid.NewGuid(),
        Feature = "RoutePlanning",
        Reason = "Lý do",
        AiSummary = "AI summary",
        TenantId = TestDb.TenantId
    };

    [Fact]
    public async Task Approve_GoiApprovalService_TraVeDto()
    {
        var approvalService = Substitute.For<IApprovalService>();
        approvalService.ApproveAsync(Arg.Any<Guid>(), TestDb.UserId, "OK", Arg.Any<CancellationToken>())
            .Returns(FakeApproval());

        var handler = new ApproveRouteHandler(approvalService, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId));

        var dto = await handler.Handle(new ApproveRouteCommand(Guid.NewGuid(), "OK"), CancellationToken.None);

        Assert.Equal("Pending", dto.Status); // status từ fake — quan trọng là mapping không lỗi
        await approvalService.Received(1).ApproveAsync(Arg.Any<Guid>(), TestDb.UserId, "OK", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_ThieuUserContext_ForbiddenException()
    {
        var handler = new ApproveRouteHandler(
            Substitute.For<IApprovalService>(), new FakeCurrentUser(TestDb.TenantId, null));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new ApproveRouteCommand(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_ReasonRong_DomainException_KhongGoiService(string reason)
    {
        var approvalService = Substitute.For<IApprovalService>();
        var handler = new RejectRouteHandler(approvalService, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new RejectRouteCommand(Guid.NewGuid(), reason, null), CancellationToken.None));

        await approvalService.DidNotReceiveWithAnyArgs()
            .RejectAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task Reject_CoReason_GoiServiceVoiReason()
    {
        var approvalService = Substitute.For<IApprovalService>();
        approvalService.RejectAsync(Arg.Any<Guid>(), TestDb.UserId, "Quá tải", "note", Arg.Any<CancellationToken>())
            .Returns(FakeApproval());

        var handler = new RejectRouteHandler(approvalService, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId));

        await handler.Handle(new RejectRouteCommand(Guid.NewGuid(), "Quá tải", "note"), CancellationToken.None);

        await approvalService.Received(1)
            .RejectAsync(Arg.Any<Guid>(), TestDb.UserId, "Quá tải", "note", Arg.Any<CancellationToken>());
    }
}
