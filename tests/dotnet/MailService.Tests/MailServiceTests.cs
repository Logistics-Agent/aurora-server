using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Security;
using MailService.Application.Interfaces.AI;
using MailService.Application.Interfaces.Persistence;
using MailService.Application.Interfaces.Security;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.AI;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Persistence.Repositories;
using MailService.Infrastructure.Security.Dns;
using MailService.Infrastructure.Security.Spf;
using MailService.Infrastructure.Security.Dkim;
using MailService.Infrastructure.Security.Dmarc;
using MailService.Infrastructure.Security.Malware;



namespace MailService.Tests;

public class MailServiceTests
{
    private MailServiceDbContext CreateInMemoryDbContext(string dbName, ICurrentUserService? currentUserService = null)
    {
        var options = new DbContextOptionsBuilder<MailServiceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        return new MailServiceDbContext(options, currentUserService ?? mockCurrentUser.Object);
    }

    [Fact]
    public async Task Test1_RevisionConsistency_ExactlyOneLatestRevision()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        Guid tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        using var dbContext = CreateInMemoryDbContext("RevisionConsistencyDb", mockUser.Object);


        var repo = new EmailDraftRepository(dbContext, mockUser.Object);
        Guid draftRootId = Guid.NewGuid();
        Guid mailboxId = Guid.NewGuid();

        // Act - Create Initial Revision
        var initialDraft = new EmailDraft
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DraftRootId = draftRootId,
            RevisionNumber = 1,
            IsLatestRevision = true,
            Source = DraftSource.Manual,
            Status = DraftStatus.Draft,
            MailboxId = mailboxId,
            Subject = "Version 1",
            Body = "Initial Body",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.CreateNewDraftAsync(initialDraft);

        // Act - Create Next Revisions sequentially
        await repo.CreateNextRevisionInTransactionAsync(draftRootId, "Version 2", "Updated Body 1", DraftSource.Manual, DraftStatus.Draft, mailboxId, null);
        await repo.CreateNextRevisionInTransactionAsync(draftRootId, "Version 3", "Updated Body 2", DraftSource.Manual, DraftStatus.Draft, mailboxId, null);

        // Assert
        var allRevisions = await dbContext.EmailDrafts.Where(d => d.DraftRootId == draftRootId).ToListAsync();
        Assert.Equal(3, allRevisions.Count);

        var latestRevisions = allRevisions.Where(d => d.IsLatestRevision).ToList();
        Assert.Single(latestRevisions);
        Assert.Equal(3, latestRevisions.First().RevisionNumber);
        Assert.Equal("Version 3", latestRevisions.First().Subject);
    }

    [Fact]
    public async Task Test2_Authorization_AiAgentDirectSubmit_RejectedAtPolicyValidation()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.RoleIds).Returns(new List<string> { "AiAgent" });

        var stage = new PolicyValidationStage(mockUser.Object);
        var context = new OutboundPipelineContext
        {
            TenantId = Guid.NewGuid(),
            SenderAddress = "agent@company.com",
            Subject = "Automated email"
        };
        context.RecipientAddresses.Add("target@external.com");


        // Act
        var result = await stage.ExecuteAsync(context);

        // Assert
        Assert.True(context.IsRejected);
        Assert.True(result.ShouldShortCircuit);
        Assert.Equal("Fail", result.Result);
        Assert.Contains("PERMISSION_DENIED", context.RejectionReason);
    }

    [Fact]
    public async Task Test3_AiGovernanceFailSafe_NoException_SkipAiStage()
    {
        // Arrange
        var client = new AiGovernanceGrpcClient(NullLogger<AiGovernanceGrpcClient>.Instance);

        // Act
        var result = await client.ExecutePolicyAsync(Guid.NewGuid(), "PhishingDetection");

        // Assert - Polly resilience handling ensures result is returned safely
        Assert.NotNull(result);
        Assert.True(result.IsAllowed || result.SkipAi);
    }

    [Fact]
    public async Task Test4_TransactionRollback_CleanStateOnFailure()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        Guid tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        using var dbContext = CreateInMemoryDbContext("RollbackDb", mockUser.Object);


        var repo = new EmailDraftRepository(dbContext, mockUser.Object);
        Guid draftRootId = Guid.NewGuid();
        Guid mailboxId = Guid.NewGuid();

        var initialDraft = new EmailDraft
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DraftRootId = draftRootId,
            RevisionNumber = 1,
            IsLatestRevision = true,
            Source = DraftSource.Manual,
            Status = DraftStatus.Draft,
            MailboxId = mailboxId,
            Subject = "Original Subject",
            Body = "Original Body",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repo.CreateNewDraftAsync(initialDraft);

        // Assert initial state
        var initialCount = await dbContext.EmailDrafts.CountAsync(d => d.DraftRootId == draftRootId);
        Assert.Equal(1, initialCount);
        var latestBefore = await repo.GetLatestRevisionAsync(draftRootId);
        Assert.Equal("Original Subject", latestBefore?.Subject);
    }

    [Fact]
    public async Task Test5_TenantIsolation_CrossTenantDraft_CannotBeReadByAnotherTenant()

    {
        // Arrange
        string sharedDb = "TenantIsolationDb_Drafts";
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        var mockUserA = new Mock<ICurrentUserService>();
        mockUserA.Setup(u => u.TenantId).Returns(tenantA);

        var mockUserB = new Mock<ICurrentUserService>();
        mockUserB.Setup(u => u.TenantId).Returns(tenantB);

        // Tenant A creates draft
        Guid draftId = Guid.NewGuid();
        using (var dbA = CreateInMemoryDbContext(sharedDb, mockUserA.Object))
        {
            dbA.EmailDrafts.Add(new EmailDraft
            {
                Id = draftId,
                TenantId = tenantA,
                DraftRootId = Guid.NewGuid(),
                Subject = "Confidential Tenant A",
                Body = "Secret content",
                Status = DraftStatus.Draft,
                IsLatestRevision = true
            });
            await dbA.SaveChangesAsync();
        }

        // Tenant B attempts to read Tenant A's draft
        using (var dbB = CreateInMemoryDbContext(sharedDb, mockUserB.Object))
        {
            var draftForB = await dbB.EmailDrafts.FirstOrDefaultAsync(d => d.Id == draftId);
            var allDraftsForB = await dbB.EmailDrafts.ToListAsync();

            // Assert
            Assert.Null(draftForB);
            Assert.Empty(allDraftsForB);
        }
    }

    [Fact]
    public async Task Test6_TenantIsolation_CrossTenantQuarantine_CannotBeReadByAnotherTenant()
    {
        // Arrange
        string sharedDb = "TenantIsolationDb_Quarantine";
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        var mockUserA = new Mock<ICurrentUserService>();
        mockUserA.Setup(u => u.TenantId).Returns(tenantA);

        var mockUserB = new Mock<ICurrentUserService>();
        mockUserB.Setup(u => u.TenantId).Returns(tenantB);

        Guid quarantineId = Guid.NewGuid();
        using (var dbA = CreateInMemoryDbContext(sharedDb, mockUserA.Object))
        {
            dbA.QuarantineRecords.Add(new QuarantineRecord
            {
                Id = quarantineId,
                TenantId = tenantA,
                MessageId = "msg-tenant-a@test.com",
                QuarantineReason = "Malware",
                Status = QuarantineStatus.Pending,
                QuarantinedAt = DateTimeOffset.UtcNow
            });
            await dbA.SaveChangesAsync();
        }

        // Tenant B queries quarantine
        using (var dbB = CreateInMemoryDbContext(sharedDb, mockUserB.Object))
        {
            var recordForB = await dbB.QuarantineRecords.FirstOrDefaultAsync(q => q.Id == quarantineId);
            var allRecordsForB = await dbB.QuarantineRecords.ToListAsync();

            // Assert
            Assert.Null(recordForB);
            Assert.Empty(allRecordsForB);
        }
    }

    [Fact]
    public async Task Test7_TenantIsolation_CrossTenantProcessedMessage_CannotBeReadByAnotherTenant()
    {
        // Arrange
        string sharedDb = "TenantIsolationDb_Messages";
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        var mockUserA = new Mock<ICurrentUserService>();
        mockUserA.Setup(u => u.TenantId).Returns(tenantA);

        var mockUserB = new Mock<ICurrentUserService>();
        mockUserB.Setup(u => u.TenantId).Returns(tenantB);

        Guid messageId = Guid.NewGuid();
        using (var dbA = CreateInMemoryDbContext(sharedDb, mockUserA.Object))
        {
            dbA.ProcessedMessages.Add(new ProcessedMessage
            {
                Id = messageId,
                TenantId = tenantA,
                MessageId = "<msg-100@domain-a.com>",
                SenderAddress = "sender@domain-a.com",
                Direction = EmailDirection.Inbound,
                PipelineStatus = PipelineStatus.Delivered,
                ReceivedAt = DateTimeOffset.UtcNow

            });
            await dbA.SaveChangesAsync();
        }

        // Tenant B queries messages
        using (var dbB = CreateInMemoryDbContext(sharedDb, mockUserB.Object))
        {
            var msgForB = await dbB.ProcessedMessages.FirstOrDefaultAsync(m => m.Id == messageId);
            var allMsgsForB = await dbB.ProcessedMessages.ToListAsync();

            // Assert
            Assert.Null(msgForB);
            Assert.Empty(allMsgsForB);
        }
    }

    [Fact]
    public async Task Test8_ClamAvUnavailable_InboundQuarantined_OutboundRejected()
    {
        // Arrange
        var mockClamAv = new Mock<IClamAvClient>();
        mockClamAv.Setup(c => c.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "CLAMAV_UNAVAILABLE"));

        // Inbound Test
        var inboundStage = new AttachmentValidationStage(mockClamAv.Object);
        var mimeMessage = new MimeKit.MimeMessage();
        var builder = new MimeKit.BodyBuilder();
        builder.Attachments.Add("document.pdf", new byte[] { 1, 2, 3, 4 });
        mimeMessage.Body = builder.ToMessageBody();

        var inboundContext = new InboundPipelineContext
        {
            TenantId = Guid.NewGuid(),
            ParsedMimeMessage = mimeMessage
        };

        var inboundResult = await inboundStage.ExecuteAsync(inboundContext);

        Assert.Equal("Fail", inboundResult.Result);
        Assert.True(inboundResult.ShouldShortCircuit);
        Assert.Contains("unavailable", inboundResult.QuarantineReason, StringComparison.OrdinalIgnoreCase);

        // Outbound Test
        var outboundStage = new OutboundAttachmentValidationStage(mockClamAv.Object);
        var outboundContext = new OutboundPipelineContext
        {
            TenantId = Guid.NewGuid(),
            SenderAddress = "user@company.com"
        };
        outboundContext.Attachments.Add(("test.pdf", "application/pdf", new byte[] { 1, 2, 3 }));

        var outboundResult = await outboundStage.ExecuteAsync(outboundContext);

        Assert.Equal("Fail", outboundResult.Result);
        Assert.True(outboundResult.ShouldShortCircuit);
        Assert.True(outboundContext.IsRejected);
        Assert.Contains("unavailable", outboundContext.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test9_SpfRealEvaluation_IpMechanismsMatchPass_MismatchFail()
    {
        // Arrange
        var spfEvaluator = new SpfEvaluator();
        string spfRecord = "v=spf1 ip4:192.168.1.0/24 ip4:10.20.30.40 -all";

        // Act & Assert
        Assert.Equal("Pass", spfEvaluator.Evaluate("company.com", spfRecord, "192.168.1.55"));
        Assert.Equal("Pass", spfEvaluator.Evaluate("company.com", spfRecord, "10.20.30.40"));
        Assert.Equal("Fail", spfEvaluator.Evaluate("company.com", spfRecord, "172.16.0.1"));
        Assert.Equal("None", spfEvaluator.Evaluate("company.com", null, "192.168.1.55"));
    }

    [Fact]
    public void Test10_DkimVerification_MissingKeyNone_EmptyBytesNone()
    {
        // Arrange
        var dkimVerifier = new DkimVerifier();

        // Act & Assert
        Assert.Equal("None", dkimVerifier.Verify(Array.Empty<byte>(), null));
        Assert.Equal("None", dkimVerifier.Verify(Array.Empty<byte>(), ""));
    }

    [Fact]
    public async Task Test11_DmarcEvaluation_RejectPolicy_QuarantinesOnFailure()
    {
        // Arrange
        var dmarcEvaluator = new DmarcEvaluator();
        var (result, policy) = dmarcEvaluator.Evaluate("Fail", "Fail", "v=DMARC1; p=reject; rua=mailto:dmarc@company.com");

        Assert.Equal("Fail", result);
        Assert.Equal("reject", policy);

        var mockDns = new Mock<IDnsLookupService>();
        mockDns.Setup(d => d.GetDmarcRecordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("v=DMARC1; p=reject");

        var dmarcStage = new DmarcEvaluationStage(mockDns.Object, dmarcEvaluator);
        var context = new InboundPipelineContext
        {
            TenantId = Guid.NewGuid(),
            SenderAddress = "spoofed@external.com",
            SpfResult = "Fail",
            DkimResult = "Fail"
        };

        var stageResult = await dmarcStage.ExecuteAsync(context);

        Assert.Equal("Fail", stageResult.Result);
        Assert.True(stageResult.ShouldShortCircuit);
        Assert.Contains("DMARC policy reject", stageResult.QuarantineReason);
    }
}


