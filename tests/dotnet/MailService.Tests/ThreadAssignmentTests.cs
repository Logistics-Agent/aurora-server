using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Shared.Constants;
using Shared.Security;
using MailService.Application.Commands.Outbound;
using MailService.Application.Commands.Threads;
using MailService.Application.Interfaces.Messaging;
using MailService.Application.Interfaces.Persistence;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Pipeline;
using MailService.Application.Queries.Threads;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Tests;

public class ThreadAssignmentTests
{
    private static MailServiceDbContext CreateInMemoryDbContext(Guid tenantId, Guid? userId = null, List<string>? roleIds = null, List<string>? permissions = null)
    {
        var options = new DbContextOptionsBuilder<MailServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.UserId).Returns(userId);
        mockUser.Setup(u => u.RoleIds).Returns(roleIds ?? [RoleConstants.Staff]);
        mockUser.Setup(u => u.Permissions).Returns(permissions ?? ["mail:read", "mail:send", "mail:create", "mail:update"]);

        return new MailServiceDbContext(options, mockUser.Object);
    }

    [Fact]
    public async Task Test01_NewInboundThread_StartsAsUnassigned()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Inbound Inquiry",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var saved = await db.EmailThreads.FindAsync(thread.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.PrimaryAssigneeUserId);
        Assert.Equal(ThreadStatus.Unassigned, saved.Status);
    }

    [Fact]
    public async Task Test02_ExistingClientReply_PreservesExistingPrimaryAssignee()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Ongoing Freight Shipment",
            PrimaryAssigneeUserId = staffA,
            AssignedAt = DateTimeOffset.UtcNow.AddHours(-2),
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        // Inbound message arrives
        var inbound = new ProcessedMessage
        {
            TenantId = tenantId,
            ThreadId = thread.Id,
            Direction = EmailDirection.Inbound,
            SenderAddress = "customer@client.com",
            Subject = "Re: Ongoing Freight Shipment"
        };
        db.ProcessedMessages.Add(inbound);
        thread.MessageCount++;
        thread.LastMessageAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var refreshed = await db.EmailThreads.FindAsync(thread.Id);
        Assert.Equal(staffA, refreshed!.PrimaryAssigneeUserId);
        Assert.Equal(ThreadStatus.InProgress, refreshed.Status);
    }

    [Fact]
    public async Task Test03_StaffClaimsUnassignedThread_BecomesPrimaryAssignee()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffA);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Customs Clearance Request",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.UserId).Returns(staffA);

        var handler = new ClaimThreadCommandHandler(db, mockUser.Object);
        var result = await handler.Handle(new ClaimThreadCommand(thread.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(staffA, result.PrimaryAssigneeUserId);
        Assert.Equal("InProgress", result.Status);

        var updated = await db.EmailThreads.FindAsync(thread.Id);
        Assert.Equal(staffA, updated!.PrimaryAssigneeUserId);
        Assert.Equal(ThreadStatus.InProgress, updated.Status);

        var history = await db.ThreadAssignmentHistories.Where(h => h.ThreadId == thread.Id).FirstOrDefaultAsync();
        Assert.NotNull(history);
        Assert.Equal(ThreadAssignmentAction.Claimed, history.Action);
        Assert.Equal(staffA, history.ToUserId);
        Assert.Equal(staffA, history.ActorUserId);
    }

    [Fact]
    public async Task Test04_TwoStaffClaimSimultaneously_SecondClaimIsRejected()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var staffB = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffA);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Urgent Shipment Booking",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        // Staff A claims first
        var mockUserA = new Mock<ICurrentUserService>();
        mockUserA.Setup(u => u.TenantId).Returns(tenantId);
        mockUserA.Setup(u => u.UserId).Returns(staffA);
        var handlerA = new ClaimThreadCommandHandler(db, mockUserA.Object);
        await handlerA.Handle(new ClaimThreadCommand(thread.Id), CancellationToken.None);

        // Staff B attempts to claim the same thread
        var mockUserB = new Mock<ICurrentUserService>();
        mockUserB.Setup(u => u.TenantId).Returns(tenantId);
        mockUserB.Setup(u => u.UserId).Returns(staffB);
        var handlerB = new ClaimThreadCommandHandler(db, mockUserB.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handlerB.Handle(new ClaimThreadCommand(thread.Id), CancellationToken.None));

        Assert.Equal("THREAD_ALREADY_ASSIGNED", ex.Message);
    }

    [Fact]
    public async Task Test05_StaffRepliesToUnassignedThread_ImplicitlyClaimsAndOutboundSucceeds()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffA);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Quotation Request",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.UserId).Returns(staffA);

        var mockDraftRepo = new Mock<IEmailDraftRepository>();
        var mockSmtp = new Mock<ISmtpDeliveryService>();
        mockSmtp.Setup(s => s.DeliverAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string, byte[])>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SmtpDeliveryResult.Success("Queued", "QUEUE-100"));

        var mockOutboxWriter = new Mock<IOutboxWriter>();
        var pipelineRunner = new OutboundPipelineRunner(
            [],
            db,
            mockOutboxWriter.Object,
            NullLogger<OutboundPipelineRunner>.Instance);

        var handler = new SubmitOutboundMessageCommandHandler(mockDraftRepo.Object, pipelineRunner, mockUser.Object, db);

        var result = await handler.Handle(new SubmitOutboundMessageCommand(
            "operations@company.com",
            ["customer@client.com"],
            "Re: Quotation Request",
            "Here is our rate quote.",
            "",
            [],
            "IDEMP-001",
            null,
            thread.Id,
            null), CancellationToken.None);

        Assert.False(result.IsRejected);

        // Thread must now be assigned to Staff A
        var updated = await db.EmailThreads.FindAsync(thread.Id);
        Assert.Equal(staffA, updated!.PrimaryAssigneeUserId);
        Assert.Equal(ThreadStatus.InProgress, updated.Status);

        // SentByUserId must be Staff A
        Assert.Equal(staffA, result.ProcessedMessage.SentByUserId);
    }

    [Fact]
    public async Task Test06_ImplicitClaimSucceeds_EvenIfOutboundSecurityPipelineRejects_AssignmentRemains()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffA);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Compliance Inquiry",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.UserId).Returns(staffA);

        var mockDraftRepo = new Mock<IEmailDraftRepository>();

        // Stage that rejects outbound message
        var mockStage = new Mock<IOutboundPipelineStage>();
        mockStage.Setup(s => s.StageName).Returns(SecurityCheckStage.PolicyValidation);
        mockStage.Setup(s => s.ExecuteAsync(It.IsAny<OutboundPipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageResult { Stage = SecurityCheckStage.PolicyValidation, Result = "Fail", ShouldShortCircuit = true });

        var mockOutboxWriter = new Mock<IOutboxWriter>();
        var pipelineRunner = new OutboundPipelineRunner(
            [mockStage.Object],
            db,
            mockOutboxWriter.Object,
            NullLogger<OutboundPipelineRunner>.Instance);

        var handler = new SubmitOutboundMessageCommandHandler(mockDraftRepo.Object, pipelineRunner, mockUser.Object, db);

        var result = await handler.Handle(new SubmitOutboundMessageCommand(
            "operations@company.com",
            ["customer@client.com"],
            "Re: Compliance Inquiry",
            "Message content",
            "",
            [],
            "IDEMP-002",
            null,
            thread.Id,
            null), CancellationToken.None);

        Assert.True(result.IsRejected);

        // Invariant: Assignment to Staff A must remain intact!
        var updated = await db.EmailThreads.FindAsync(thread.Id);
        Assert.Equal(staffA, updated!.PrimaryAssigneeUserId);
    }

    [Fact]
    public async Task Test07_DifferentNormalStaffRepliesToAssignedThread_IsDenied()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var staffB = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffB);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Assigned Cargo Query",
            PrimaryAssigneeUserId = staffA, // Assigned to Staff A
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        // Staff B attempts to reply
        var mockUserB = new Mock<ICurrentUserService>();
        mockUserB.Setup(u => u.TenantId).Returns(tenantId);
        mockUserB.Setup(u => u.UserId).Returns(staffB);
        mockUserB.Setup(u => u.RoleIds).Returns([RoleConstants.Staff]);
        mockUserB.Setup(u => u.Permissions).Returns(["mail:send"]);

        var mockDraftRepo = new Mock<IEmailDraftRepository>();
        var mockOutboxWriter = new Mock<IOutboxWriter>();
        var pipelineRunner = new OutboundPipelineRunner(
            [],
            db,
            mockOutboxWriter.Object,
            NullLogger<OutboundPipelineRunner>.Instance);

        var handler = new SubmitOutboundMessageCommandHandler(mockDraftRepo.Object, pipelineRunner, mockUserB.Object, db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitOutboundMessageCommand(
                "operations@company.com",
                ["customer@client.com"],
                "Re: Assigned Cargo Query",
                "Unauthorized reply",
                "",
                [],
                "IDEMP-003",
                null,
                thread.Id,
                null), CancellationToken.None));

        Assert.Equal("THREAD_ASSIGNED_TO_ANOTHER_STAFF", ex.Message);
    }

    [Fact]
    public async Task Test08_ManagerReadsAnyTenantThread_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, managerId);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "High Value Cargo",
            PrimaryAssigneeUserId = staffA,
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockManager = new Mock<ICurrentUserService>();
        mockManager.Setup(u => u.TenantId).Returns(tenantId);
        mockManager.Setup(u => u.UserId).Returns(managerId);
        mockManager.Setup(u => u.RoleIds).Returns([RoleConstants.Manager]);
        mockManager.Setup(u => u.Permissions).Returns(["mail:read", "mail:assign"]);

        var handler = new GetThreadQueryHandler(db, mockManager.Object);
        var result = await handler.Handle(new GetThreadQuery(thread.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(thread.Id, result.Thread.Id);
    }

    [Fact]
    public async Task Test09_StaffReadsAnotherStaffsThread_IsDenied()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var staffB = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffB);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Private Customer Dispute",
            PrimaryAssigneeUserId = staffA,
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockStaffB = new Mock<ICurrentUserService>();
        mockStaffB.Setup(u => u.TenantId).Returns(tenantId);
        mockStaffB.Setup(u => u.UserId).Returns(staffB);
        mockStaffB.Setup(u => u.RoleIds).Returns([RoleConstants.Staff]);
        mockStaffB.Setup(u => u.Permissions).Returns(["mail:read"]);

        var handler = new GetThreadQueryHandler(db, mockStaffB.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetThreadQuery(thread.Id), CancellationToken.None));

        Assert.Equal("THREAD_ASSIGNED_TO_ANOTHER_STAFF", ex.Message);
    }

    [Fact]
    public async Task Test10_StaffReadsUnassignedWorkPool_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffA);

        var threadUnassigned = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Unassigned Pool 1",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        var threadStaffA = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "My Work 1",
            PrimaryAssigneeUserId = staffA,
            Status = ThreadStatus.InProgress
        };
        var threadStaffB = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Other Work",
            PrimaryAssigneeUserId = Guid.NewGuid(),
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.AddRange(threadUnassigned, threadStaffA, threadStaffB);
        await db.SaveChangesAsync();

        var mockStaffA = new Mock<ICurrentUserService>();
        mockStaffA.Setup(u => u.TenantId).Returns(tenantId);
        mockStaffA.Setup(u => u.UserId).Returns(staffA);
        mockStaffA.Setup(u => u.RoleIds).Returns([RoleConstants.Staff]);
        mockStaffA.Setup(u => u.Permissions).Returns(["mail:read"]);

        var handler = new ListThreadsQueryHandler(db, mockStaffA.Object);

        // Scope = UNASSIGNED
        var unassignedList = await handler.Handle(new ListThreadsQuery(null, 20, null, "UNASSIGNED"), CancellationToken.None);
        Assert.Single(unassignedList);
        Assert.Equal("Unassigned Pool 1", unassignedList[0].Subject);

        // Scope = MY_WORK
        var myWorkList = await handler.Handle(new ListThreadsQuery(null, 20, null, "MY_WORK"), CancellationToken.None);
        Assert.Single(myWorkList);
        Assert.Equal("My Work 1", myWorkList[0].Subject);
    }

    [Fact]
    public async Task Test11_ManagerReassignsThread_SameThreadId_NoEmailCopied_StaffBGainsAccess()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var staffB = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, managerId);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Cargo Reassignment Flow",
            PrimaryAssigneeUserId = staffA,
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockManager = new Mock<ICurrentUserService>();
        mockManager.Setup(u => u.TenantId).Returns(tenantId);
        mockManager.Setup(u => u.UserId).Returns(managerId);
        mockManager.Setup(u => u.RoleIds).Returns([RoleConstants.Manager]);
        mockManager.Setup(u => u.Permissions).Returns(["mail:assign"]);

        var reassignHandler = new ReassignThreadCommandHandler(db, mockManager.Object);
        var result = await reassignHandler.Handle(new ReassignThreadCommand(thread.Id, staffB, "Staff A is on leave"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(thread.Id, result.ThreadId);
        Assert.Equal(staffB, result.PrimaryAssigneeUserId);

        // Invariant: Verify thread is unchanged, only PrimaryAssignee changed
        var updatedThread = await db.EmailThreads.FindAsync(thread.Id);
        Assert.Equal(staffB, updatedThread!.PrimaryAssigneeUserId);

        // Verify assignment history
        var history = await db.ThreadAssignmentHistories.Where(h => h.ThreadId == thread.Id).FirstOrDefaultAsync();
        Assert.NotNull(history);
        Assert.Equal(ThreadAssignmentAction.Reassigned, history.Action);
        Assert.Equal(staffA, history.FromUserId);
        Assert.Equal(staffB, history.ToUserId);
        Assert.Equal(managerId, history.ActorUserId);
        Assert.Equal("Staff A is on leave", history.Reason);

        // Now Staff B can read the thread
        var mockStaffB = new Mock<ICurrentUserService>();
        mockStaffB.Setup(u => u.TenantId).Returns(tenantId);
        mockStaffB.Setup(u => u.UserId).Returns(staffB);
        mockStaffB.Setup(u => u.RoleIds).Returns([RoleConstants.Staff]);
        mockStaffB.Setup(u => u.Permissions).Returns(["mail:read"]);

        var getHandler = new GetThreadQueryHandler(db, mockStaffB.Object);
        var getResult = await getHandler.Handle(new GetThreadQuery(thread.Id), CancellationToken.None);
        Assert.NotNull(getResult);
        Assert.Equal(staffB, getResult.Thread.PrimaryAssigneeUserId);
    }

    [Fact]
    public async Task Test12_ManagerUnassignsThread_ReturnsToUnassignedWorkPool()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, managerId);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "General Freight Inquiry",
            PrimaryAssigneeUserId = staffA,
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockManager = new Mock<ICurrentUserService>();
        mockManager.Setup(u => u.TenantId).Returns(tenantId);
        mockManager.Setup(u => u.UserId).Returns(managerId);

        var unassignHandler = new UnassignThreadCommandHandler(db, mockManager.Object);
        var result = await unassignHandler.Handle(new UnassignThreadCommand(thread.Id, "Back to pool"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Unassigned", result.Status);

        var updated = await db.EmailThreads.FindAsync(thread.Id);
        Assert.Null(updated!.PrimaryAssigneeUserId);
        Assert.Equal(ThreadStatus.Unassigned, updated.Status);

        var history = await db.ThreadAssignmentHistories.Where(h => h.ThreadId == thread.Id).FirstOrDefaultAsync();
        Assert.NotNull(history);
        Assert.Equal(ThreadAssignmentAction.Unassigned, history.Action);
        Assert.Equal(staffA, history.FromUserId);
        Assert.Null(history.ToUserId);
    }

    [Fact]
    public async Task Test13_TenantIsolation_TenantACannotAccessTenantBThread()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var staffA = Guid.NewGuid();

        using var db = CreateInMemoryDbContext(tenantA, staffA);

        var threadTenantB = new EmailThread
        {
            TenantId = tenantB,
            MailboxId = Guid.NewGuid(),
            Subject = "Tenant B Secret Email",
            PrimaryAssigneeUserId = null,
            Status = ThreadStatus.Unassigned
        };
        db.EmailThreads.Add(threadTenantB);
        await db.SaveChangesAsync();

        var mockStaffA = new Mock<ICurrentUserService>();
        mockStaffA.Setup(u => u.TenantId).Returns(tenantA);
        mockStaffA.Setup(u => u.UserId).Returns(staffA);

        var getHandler = new GetThreadQueryHandler(db, mockStaffA.Object);
        var result = await getHandler.Handle(new GetThreadQuery(threadTenantB.Id), CancellationToken.None);

        Assert.Null(result); // Must not find thread belonging to Tenant B
    }

    [Fact]
    public async Task Test14_StaffQueriesScopeAll_IsDenied()
    {
        var tenantId = Guid.NewGuid();
        var staffA = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, staffA);

        var mockStaffA = new Mock<ICurrentUserService>();
        mockStaffA.Setup(u => u.TenantId).Returns(tenantId);
        mockStaffA.Setup(u => u.UserId).Returns(staffA);
        mockStaffA.Setup(u => u.RoleIds).Returns([RoleConstants.Staff]);
        mockStaffA.Setup(u => u.Permissions).Returns(["mail:read"]);

        var handler = new ListThreadsQueryHandler(db, mockStaffA.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ListThreadsQuery(null, 20, null, "ALL"), CancellationToken.None));

        Assert.Contains("FORBIDDEN_SCOPE_ALL", ex.Message);
    }

    [Fact]
    public async Task Test15_SentByUserId_MustBeAuthoritativeFromAuthenticatedContext()
    {
        var tenantId = Guid.NewGuid();
        var authenticatedStaffId = Guid.NewGuid();
        using var db = CreateInMemoryDbContext(tenantId, authenticatedStaffId);

        var thread = new EmailThread
        {
            TenantId = tenantId,
            MailboxId = Guid.NewGuid(),
            Subject = "Authoritative Sender Test",
            PrimaryAssigneeUserId = authenticatedStaffId,
            Status = ThreadStatus.InProgress
        };
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.UserId).Returns(authenticatedStaffId);

        var mockDraftRepo = new Mock<IEmailDraftRepository>();
        var mockOutboxWriter = new Mock<IOutboxWriter>();
        var pipelineRunner = new OutboundPipelineRunner(
            [],
            db,
            mockOutboxWriter.Object,
            NullLogger<OutboundPipelineRunner>.Instance);

        var handler = new SubmitOutboundMessageCommandHandler(mockDraftRepo.Object, pipelineRunner, mockUser.Object, db);

        var result = await handler.Handle(new SubmitOutboundMessageCommand(
            "operations@company.com",
            ["client@external.com"],
            "Authoritative Sender Verify",
            "Checking sender stamping",
            "",
            [],
            "IDEMP-AUTH-1",
            null,
            thread.Id,
            null), CancellationToken.None);

        Assert.False(result.IsRejected);
        // ProcessedMessage.SentByUserId must strictly match authenticated user context
        Assert.Equal(authenticatedStaffId, result.ProcessedMessage.SentByUserId);

        var savedMessage = await db.ProcessedMessages.FirstOrDefaultAsync(m => m.ThreadId == thread.Id);
        Assert.NotNull(savedMessage);
        Assert.Equal(authenticatedStaffId, savedMessage.SentByUserId);
    }
}
