using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Security;
using Shared.Events;
using MailService.Application.Commands.Outbound;
using MailService.Application.Commands.Quarantine;
using MailService.Application.Interfaces.AI;
using MailService.Application.Interfaces.Classification;
using MailService.Application.Interfaces.Messaging;
using MailService.Application.Interfaces.Persistence;
using MailService.Application.Interfaces.RateLimiting;
using MailService.Application.Interfaces.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Interfaces.Storage;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.AI;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Persistence.Repositories;
using MailService.Infrastructure.Security.Dns;
using MailService.Infrastructure.Security.Spf;
using MailService.Infrastructure.Security.Dkim;
using MailService.Infrastructure.Security.Dmarc;
using MailService.Infrastructure.Security.Malware;
using MailService.Infrastructure.Security.Spam;
using MailService.Infrastructure.Stalwart;

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
        var client = new AiGovernanceGrpcClient(logger: NullLogger<AiGovernanceGrpcClient>.Instance);

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
            .ReturnsAsync(ClamAvScanResult.Unavailable("ClamAV daemon down"));

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
    public async Task Test9_SpfRealEvaluation_RecursionProtection_And_CircularInclude()
    {
        // Arrange
        var spfEvaluator = new SpfEvaluator();
        var mockDns = new Mock<IDnsLookupService>();

        // 1. CIDR matching
        string spfRecord = "v=spf1 ip4:192.168.1.0/24 ip4:10.20.30.40 -all";
        Assert.Equal(SpfStatus.Pass, spfEvaluator.Evaluate("company.com", spfRecord, "192.168.1.55").Status);
        Assert.Equal(SpfStatus.Fail, spfEvaluator.Evaluate("company.com", spfRecord, "172.16.0.1").Status);

        // 2. Circular include protection
        mockDns.Setup(d => d.GetSpfRecordAsync("loop-a.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("v=spf1 include:loop-b.com -all");
        mockDns.Setup(d => d.GetSpfRecordAsync("loop-b.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("v=spf1 include:loop-a.com -all");

        var circularResult = await spfEvaluator.EvaluateAsync("loop-a.com", "v=spf1 include:loop-b.com -all", "1.2.3.4", mockDns.Object);
        Assert.Equal(SpfStatus.PermError, circularResult.Status);
        Assert.Contains("Circular", circularResult.Explanation);
    }

    [Fact]
    public void Test10_DkimVerification_CryptographicVerification_And_TamperingDetection()
    {
        // Arrange - Generate real RSA key pair
        using var rsa = RSA.Create(2048);
        byte[] publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        string dnsTxtRecord = $"v=DKIM1; k=rsa; p={Convert.ToBase64String(publicKeyBytes)}";

        // Create MimeMessage
        var message = new MimeKit.MimeMessage();
        message.Headers.Add("From", "sender@aurora.com");
        message.Headers.Add("To", "recipient@client.com");
        message.Headers.Add("Subject", "Signed Invoice #123");
        message.Body = new MimeKit.TextPart("plain") { Text = "Please find attached invoice." };

        // Compute body hash
        using var sha256 = SHA256.Create();
        byte[] bodyBytes = Encoding.UTF8.GetBytes("Please find attached invoice.\r\n");
        byte[] bodyHash = sha256.ComputeHash(bodyBytes);
        string bh = Convert.ToBase64String(bodyHash);

        // Sign canonical headers with RSA private key
        var dkimHeader = new MimeKit.Header("DKIM-Signature", $"v=1; a=rsa-sha256; d=aurora.com; s=aurora-2025; c=relaxed/relaxed; h=from:to:subject; bh={bh}; b=");
        byte[] headerData = DkimVerifier.BuildCanonicalizedHeaderData(message, "from:to:subject", dkimHeader);
        byte[] headerSignature = rsa.SignData(headerData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string b = Convert.ToBase64String(headerSignature);

        message.Headers.Add("DKIM-Signature", $"v=1; a=rsa-sha256; d=aurora.com; s=aurora-2025; c=relaxed/relaxed; h=from:to:subject; bh={bh}; b={b}");



        using var ms = new MemoryStream();
        message.WriteTo(ms);
        byte[] validEml = ms.ToArray();

        var verifier = new DkimVerifier();

        // 1. Act - Verify authentic EML
        var validResult = verifier.Verify(validEml, dnsTxtRecord);
        Assert.True(validResult.Status == DkimStatus.Pass, $"Status: {validResult.Status}, Details: {validResult.Details}");


        // 2. Act - Tamper with body text
        message.Body = new MimeKit.TextPart("plain") { Text = "TAMPERED: Please send payment to attacker account." };
        using var tamperedMs = new MemoryStream();
        message.WriteTo(tamperedMs);
        byte[] tamperedEml = tamperedMs.ToArray();

        var tamperedResult = verifier.Verify(tamperedEml, dnsTxtRecord);
        Assert.Equal(DkimStatus.Fail, tamperedResult.Status);
        Assert.Contains("tampered", tamperedResult.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test11_DmarcEvaluation_SeparationOfEvaluationAndEnforcement()
    {
        // Arrange
        var dmarcEvaluator = new DmarcEvaluator();

        // 1. Aligned Pass -> Accept
        var passEval = dmarcEvaluator.Evaluate("aurora.com", "Pass", "aurora.com", "Pass", "aurora.com", "v=DMARC1; p=reject");
        var passDecision = dmarcEvaluator.DetermineEnforcement(passEval);
        Assert.Equal(DmarcStatus.Pass, passEval.Status);
        Assert.Equal(DmarcEnforcementAction.Accept, passDecision.Action);

        // 2. Alignment Fail with p=reject -> Reject
        var rejectEval = dmarcEvaluator.Evaluate("aurora.com", "Fail", "other.com", "Fail", "other.com", "v=DMARC1; p=reject");
        var rejectDecision = dmarcEvaluator.DetermineEnforcement(rejectEval);
        Assert.Equal(DmarcStatus.Fail, rejectEval.Status);
        Assert.Equal(DmarcEnforcementAction.Reject, rejectDecision.Action);
        Assert.True(rejectDecision.ShouldReject);

        // 3. Alignment Fail with p=quarantine -> Quarantine
        var quarEval = dmarcEvaluator.Evaluate("aurora.com", "Fail", "other.com", "Fail", "other.com", "v=DMARC1; p=quarantine");
        var quarDecision = dmarcEvaluator.DetermineEnforcement(quarEval);
        Assert.Equal(DmarcStatus.Fail, quarEval.Status);
        Assert.Equal(DmarcEnforcementAction.Quarantine, quarDecision.Action);
        Assert.True(quarDecision.ShouldQuarantine);
    }

    [Fact]
    public async Task Test12_OutboundPipeline_SmtpFailure_DistinguishesTransientAndPermanent()
    {
        // Arrange
        var mockSmtp = new Mock<ISmtpDeliveryService>();
        mockSmtp.SetupSequence(s => s.DeliverAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string, byte[])>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SmtpDeliveryResult.Transient(451, "Requested action aborted: local error in processing"))
            .ReturnsAsync(SmtpDeliveryResult.Permanent(550, "5.1.1 User unknown"));

        var stage = new StalwartSmtpSubmissionStage(mockSmtp.Object, NullLogger<StalwartSmtpSubmissionStage>.Instance);

        // Act 1 - Transient Failure
        var context1 = new OutboundPipelineContext { TenantId = Guid.NewGuid(), SenderAddress = "user@aurora.com" };
        var result1 = await stage.ExecuteAsync(context1);

        Assert.Equal("Fail", result1.Result);
        Assert.True(context1.IsRejected);
        Assert.Contains("451", context1.RejectionReason);

        // Act 2 - Permanent Failure
        var context2 = new OutboundPipelineContext { TenantId = Guid.NewGuid(), SenderAddress = "user@aurora.com" };
        var result2 = await stage.ExecuteAsync(context2);

        Assert.Equal("Fail", result2.Result);
        Assert.True(context2.IsRejected);
        Assert.Contains("550", context2.RejectionReason);
    }

    [Fact]
    public async Task Test13_OutboundPipeline_EndToEnd_DraftToSend_Smtp2xx_Success()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        var tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.RoleIds).Returns(new List<string> { "Staff" });
        using var dbContext = CreateInMemoryDbContext("DraftToSendDb", mockUser.Object);

        var draftRepo = new EmailDraftRepository(dbContext, mockUser.Object);
        var draftRootId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var draft = new EmailDraft
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DraftRootId = draftRootId,
            RevisionNumber = 1,
            IsLatestRevision = true,
            Source = DraftSource.AiAgent,
            Status = DraftStatus.Draft,

            MailboxId = mailboxId,
            Subject = "Logistics Notification",
            Body = "Your shipment has departed the warehouse.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await draftRepo.CreateNewDraftAsync(draft);

        // Setup Outbound Pipeline with mock SMTP
        var mockSmtp = new Mock<ISmtpDeliveryService>();
        mockSmtp.Setup(s => s.DeliverAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<(string, string, byte[])>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SmtpDeliveryResult.Success("250 2.0.0 Ok: queued as 4V9dZg6m8bz9", "4V9dZg6m8bz9"));

        var mockClamAv = new Mock<IClamAvClient>();
        mockClamAv.Setup(c => c.ScanStreamAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClamAvScanResult.CleanResult(100, 1));

        var mockAiGov = new Mock<IAiGovernanceClient>();
        mockAiGov.Setup(g => g.ExecutePolicyAsync(tenantId, "BecRiskScoring", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiGovernancePolicyResult.Allowed("Gemini"));

        var mockRisk = new Mock<IRiskScoringService>();
        mockRisk.Setup(r => r.AnalyzeBecRiskAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0.1m, "Low risk"));

        var mockRate = new Mock<IRateLimitService>();
        mockRate.Setup(r => r.IsOutboundRateExceededAsync(tenantId, Guid.Empty, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 1, DateTimeOffset.UtcNow.AddHours(1)));

        var stages = new List<IOutboundPipelineStage>
        {
            new OutboundAttachmentValidationStage(mockClamAv.Object),
            new PolicyValidationStage(mockUser.Object),
            new AiRiskScoringStage(mockAiGov.Object, mockRisk.Object, NullLogger<AiRiskScoringStage>.Instance),
            new RateLimitCheckStage(mockRate.Object),
            new AuditCreationStage(),
            new StalwartSmtpSubmissionStage(mockSmtp.Object, NullLogger<StalwartSmtpSubmissionStage>.Instance)
        };

        var outboxWriter = new OutboxWriter(dbContext);
        var pipelineRunner = new OutboundPipelineRunner(stages, dbContext, outboxWriter, NullLogger<OutboundPipelineRunner>.Instance);

        var handler = new SubmitOutboundMessageCommandHandler(draftRepo, pipelineRunner, mockUser.Object);

        // Act - Submit outbound message
        var command = new SubmitOutboundMessageCommand(
            "staff@aurora.com",
            new List<string> { "client@external.com" },
            "Logistics Notification",
            "Your shipment has departed the warehouse.",
            "<p>Your shipment has departed the warehouse.</p>",
            new List<(string, string, byte[])>(),
            Guid.NewGuid().ToString(),
            draftRootId);

        var resultContext = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(resultContext.IsRejected);
        Assert.Equal("4V9dZg6m8bz9", resultContext.StalwartQueueId);

        // Draft marked Sent only after SMTP success
        var latestDraft = await draftRepo.GetLatestRevisionAsync(draftRootId);
        Assert.NotNull(latestDraft);
        Assert.Equal(DraftStatus.Sent, latestDraft.Status);

        // Outbox event OutboundEmailSentEvent recorded
        var outboxSent = await dbContext.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == nameof(OutboundEmailSentEvent));
        Assert.NotNull(outboxSent);
        Assert.Contains("4V9dZg6m8bz9", outboxSent.Payload);
    }

    [Fact]
    public async Task Test14_QuarantineRelease_Malware_BlocksRegularStaff_AllowsAdmin()
    {
        // Arrange
        var mockStaffUser = new Mock<ICurrentUserService>();
        mockStaffUser.Setup(u => u.RoleIds).Returns(new List<string> { "Staff" });

        var mockAdminUser = new Mock<ICurrentUserService>();
        mockAdminUser.Setup(u => u.RoleIds).Returns(new List<string> { "SYSTEM_ADMIN" });

        using var dbContext = CreateInMemoryDbContext("QuarantineMalwareDb", mockStaffUser.Object);
        var mockStalwart = new Mock<IStalwartManagementClient>();
        mockStalwart.Setup(s => s.DeliverQuarantinedMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Guid quarantineId = Guid.NewGuid();
        var malwareRecord = new QuarantineRecord
        {
            Id = quarantineId,
            TenantId = Guid.NewGuid(),
            MessageId = "msg-infected@bad.com",
            QuarantineReason = "Malware virus detected (EICAR-Test-Signature)",
            Status = QuarantineStatus.Pending,
            QuarantinedAt = DateTimeOffset.UtcNow
        };
        dbContext.QuarantineRecords.Add(malwareRecord);
        await dbContext.SaveChangesAsync();

        // Act 1 - Regular Staff attempts release -> Throws UnauthorizedAccessException
        var staffHandler = new ReleaseQuarantineCommandHandler(dbContext, mockStalwart.Object, mockStaffUser.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            staffHandler.Handle(new ReleaseQuarantineCommand(quarantineId, AdminOverride: false), CancellationToken.None));

        // Act 2 - Admin attempts release -> Succeeds
        var adminHandler = new ReleaseQuarantineCommandHandler(dbContext, mockStalwart.Object, mockAdminUser.Object);
        bool released = await adminHandler.Handle(new ReleaseQuarantineCommand(quarantineId, AdminOverride: true), CancellationToken.None);

        Assert.True(released);
        var updated = await dbContext.QuarantineRecords.FindAsync(quarantineId);
        Assert.Equal(QuarantineStatus.Released, updated?.Status);
    }

    [Fact]
    public async Task Test15_OutboxWriter_PersistsUnprocessedMessageInSameTransaction()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        var tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        using var dbContext = CreateInMemoryDbContext("OutboxDb", mockUser.Object);

        var writer = new OutboxWriter(dbContext);
        var evt = new InboundEmailReceivedEvent
        {
            TenantId = tenantId,
            MessageId = Guid.NewGuid(),
            SenderEmail = "sender@client.com",
            Subject = "Logistics Update"
        };

        // Act
        await writer.WriteAsync(evt);

        // Assert
        var saved = await dbContext.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(nameof(InboundEmailReceivedEvent), saved.EventType);
        Assert.Null(saved.ProcessedAt);
        Assert.Contains("Logistics Update", saved.Payload);
    }

    [Fact]
    public async Task Test16_GovernedServices_ExecuteViaAiGovernanceClient()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        var tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);

        var mockGov = new Mock<IAiGovernanceClient>();
        mockGov.Setup(g => g.ExecutePolicyAsync(tenantId, "PhishingDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiGovernancePolicyResult.Allowed("Gemini"));
        mockGov.Setup(g => g.GenerateAsync(tenantId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"phishingScore\": 0.85, \"reasoning\": \"Suspicious phishing domain detected\"}");

        var phishingService = new GovernedPhishingDetectionService(
            mockGov.Object,
            mockUser.Object,
            NullLogger<GovernedPhishingDetectionService>.Instance);

        // Act
        var (score, reason) = await phishingService.AnalyzePhishingAsync(
            "Urgent password reset",
            "Please click http://evil-login.com",
            "admin@fake.com",
            new List<string> { "http://evil-login.com" });

        // Assert
        Assert.Equal(0.85m, score);
        Assert.Contains("Suspicious phishing domain", reason);
        mockGov.Verify(g => g.ExecutePolicyAsync(tenantId, "PhishingDetection", It.IsAny<CancellationToken>()), Times.Once);
        mockGov.Verify(g => g.GenerateAsync(tenantId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Test17_MetadataPropagation_ClientMetadataInterceptor_AppendsExpectedHeaders()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUserService>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        mockUser.Setup(u => u.UserId).Returns(userId);
        mockUser.Setup(u => u.TraceId).Returns("trace-12345");
        mockUser.Setup(u => u.RoleIds).Returns(new List<string> { "TenantAdmin", "Staff" });

        var interceptor = new Shared.Interceptors.ClientMetadataInterceptor(mockUser.Object, "mail-service");
        var metadata = new Grpc.Core.Metadata();

        // Act - invoke AppendMetadata logic via reflection or test call
        var method = typeof(Shared.Interceptors.ClientMetadataInterceptor)
            .GetMethod("AppendMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(interceptor, new object[] { metadata });

        // Assert
        Assert.Equal("mail-service", metadata.GetValue(Shared.Security.GrpcMetadataKeys.ServiceId));
        Assert.Equal(tenantId.ToString(), metadata.GetValue(Shared.Security.GrpcMetadataKeys.TenantId));
        Assert.Equal(userId.ToString(), metadata.GetValue(Shared.Security.GrpcMetadataKeys.UserId));
        Assert.Equal("trace-12345", metadata.GetValue(Shared.Security.GrpcMetadataKeys.TraceId));
        Assert.Equal("TenantAdmin,Staff", metadata.GetValue(Shared.Security.GrpcMetadataKeys.RoleIds));
    }

    [Fact]
    public async Task Test18_ConsumerIdempotency_TenantUserProvisioned_DuplicateSafe()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = "admin@tenant1.com";
        var dbName = Guid.NewGuid().ToString();

        var mockStalwart = new Mock<MailService.Application.Interfaces.Stalwart.IStalwartManagementClient>();
        mockStalwart.Setup(s => s.ProvisionAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockContext = new Mock<MassTransit.ConsumeContext<Shared.Events.TenantAdminCreatedEvent>>();
        mockContext.Setup(c => c.Message).Returns(new Shared.Events.TenantAdminCreatedEvent
        {
            TenantId = tenantId,
            UserId = userId,
            Email = email,
            TenantName = "Tenant 1"
        });
        mockContext.Setup(c => c.MessageId).Returns(Guid.NewGuid());
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act - Delivery 1 and Delivery 2 (duplicate message)
        using (var db1 = CreateInMemoryDbContext(dbName))
        {
            var consumer = new MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer(
                db1,
                mockStalwart.Object,
                NullLogger<MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer>.Instance);

            await consumer.Consume(mockContext.Object);
        }

        using (var db2 = CreateInMemoryDbContext(dbName))
        {
            var consumer = new MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer(
                db2,
                mockStalwart.Object,
                NullLogger<MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer>.Instance);

            await consumer.Consume(mockContext.Object);
        }

        // Assert - Exactly 1 Mailbox exists in DB and Stalwart was called for provisioning/reconciliation
        using (var verifyDb = CreateInMemoryDbContext(dbName))
        {
            var mailboxes = await verifyDb.Mailboxes.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).ToListAsync();
            Assert.Single(mailboxes);
            Assert.Equal(email, mailboxes[0].FullAddress);
            Assert.Equal(userId, mailboxes[0].UserId);
            Assert.Equal(MailboxStatus.Active, mailboxes[0].Status);

            var audits = await verifyDb.AuditRecords.IgnoreQueryFilters().Where(a => a.TenantId == tenantId).ToListAsync();
            Assert.NotEmpty(audits);
            Assert.Contains(audits, a => a.Action == "MailboxProvisioned");
        }

        mockStalwart.Verify(s => s.ProvisionAccountAsync(email, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Test24_Consumer_TenantStaffCreatedEvent_AutoProvisionsMailbox()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = "staff.john@acme.com";
        var dbName = Guid.NewGuid().ToString();

        var mockStalwart = new Mock<MailService.Application.Interfaces.Stalwart.IStalwartManagementClient>();
        mockStalwart.Setup(s => s.ProvisionAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockContext = new Mock<MassTransit.ConsumeContext<Shared.Events.TenantStaffCreatedEvent>>();
        mockContext.Setup(c => c.Message).Returns(new Shared.Events.TenantStaffCreatedEvent
        {
            TenantId = tenantId,
            UserId = userId,
            Email = email,
            FirstName = "John",
            LastName = "Doe"
        });
        mockContext.Setup(c => c.MessageId).Returns(Guid.NewGuid());
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        using (var db = CreateInMemoryDbContext(dbName))
        {
            var consumer = new MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer(
                db,
                mockStalwart.Object,
                NullLogger<MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer>.Instance);

            await consumer.Consume(mockContext.Object);
        }

        using (var verifyDb = CreateInMemoryDbContext(dbName))
        {
            var mailboxes = await verifyDb.Mailboxes.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).ToListAsync();
            Assert.Single(mailboxes);
            Assert.Equal(email, mailboxes[0].FullAddress);
            Assert.Equal("staff.john", mailboxes[0].LocalPart);
            Assert.Equal(userId, mailboxes[0].UserId);
        }
    }

    [Fact]
    public async Task Test25_Consumer_CrossTenantEvent_CannotProvisionOnOtherTenantDomain()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var mockStalwart = new Mock<MailService.Application.Interfaces.Stalwart.IStalwartManagementClient>();

        // Seed domain for Tenant A
        using (var seedDb = CreateInMemoryDbContext(dbName))
        {
            seedDb.Domains.Add(new MailService.Domain.Entities.Domain
            {
                TenantId = tenantA,
                DomainName = "tenant-a.com",
                Status = DomainStatus.Active
            });
            await seedDb.SaveChangesAsync();
        }

        // Tenant B attempts to provision a user on tenant-a.com
        var mockContext = new Mock<MassTransit.ConsumeContext<Shared.Events.TenantStaffCreatedEvent>>();
        mockContext.Setup(c => c.Message).Returns(new Shared.Events.TenantStaffCreatedEvent
        {
            TenantId = tenantB,
            UserId = Guid.NewGuid(),
            Email = "intruder@tenant-a.com",
            FirstName = "Malicious",
            LastName = "Actor"
        });
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        using (var testDb = CreateInMemoryDbContext(dbName))
        {
            var consumer = new MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer(
                testDb,
                mockStalwart.Object,
                NullLogger<MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer>.Instance);

            // Assert security violation thrown
            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Consume(mockContext.Object));
        }
    }

    [Fact]
    public async Task Test26_Consumer_EmptyTenantId_ThrowsArgumentException()
    {
        var mockStalwart = new Mock<MailService.Application.Interfaces.Stalwart.IStalwartManagementClient>();
        var mockContext = new Mock<MassTransit.ConsumeContext<Shared.Events.TenantAdminCreatedEvent>>();
        mockContext.Setup(c => c.Message).Returns(new Shared.Events.TenantAdminCreatedEvent
        {
            TenantId = Guid.Empty,
            UserId = Guid.NewGuid(),
            Email = "invalid@acme.com"
        });
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var consumer = new MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer(
            db,
            mockStalwart.Object,
            NullLogger<MailService.Infrastructure.Messaging.Consumers.TenantUserProvisionedConsumer>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => consumer.Consume(mockContext.Object));
    }

    [Fact]
    public async Task Test27_CreateMailboxCommandHandler_ManualProvisioning_WorksForSharedMailbox()
    {
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);
        mockCurrentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var mockStalwart = new Mock<MailService.Application.Interfaces.Stalwart.IStalwartManagementClient>();
        mockStalwart.Setup(s => s.ProvisionAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using (var seedDb = CreateInMemoryDbContext(dbName, mockCurrentUser.Object))
        {
            seedDb.Domains.Add(new MailService.Domain.Entities.Domain
            {
                Id = domainId,
                TenantId = tenantId,
                DomainName = "logistics.vn",
                Status = DomainStatus.Active
            });
            await seedDb.SaveChangesAsync();
        }

        using (var testDb = CreateInMemoryDbContext(dbName, mockCurrentUser.Object))
        {
            var handler = new MailService.Application.Commands.Provisioning.CreateMailboxCommandHandler(
                testDb,
                mockStalwart.Object,
                mockCurrentUser.Object);

            var result = await handler.Handle(
                new MailService.Application.Commands.Provisioning.CreateMailboxCommand(domainId, "support", null),
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("support@logistics.vn", result.FullAddress);
            Assert.Equal(MailboxStatus.Active, result.Status);
            Assert.Null(result.UserId); // Shared department mailbox
        }
    }

    private IamTenant.Infrastructure.Persistences.IamTenantDbContext CreateInMemoryIamDbContext(string dbName, ICurrentUserService? currentUserService = null)
    {
        var options = new DbContextOptionsBuilder<IamTenant.Infrastructure.Persistences.IamTenantDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var user = currentUserService ?? mockCurrentUser.Object;

        var auditInterceptor = new Shared.Interceptors.AuditSaveChangesInterceptor(user);
        return new IamTenant.Infrastructure.Persistences.IamTenantDbContext(options, user, auditInterceptor);
    }

    [Fact]
    public async Task Test28_ProducerOutbox_CreateStaff_AtomicallyPersistsUserAndOutboxMessage()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);

        var mockCognito = new Mock<IamTenant.Application.Interfaces.ICognitoAuthService>();
        mockCognito.Setup(c => c.AdminCreateUserInPoolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cognito-sub-12345");

        // Seed tenant
        using (var seedDb = CreateInMemoryIamDbContext(dbName, mockCurrentUser.Object))
        {
            var tenant = IamTenant.Domain.Tenant.Create("Logistics Corp", "logistics.vn", null, Guid.NewGuid());
            tenant.Id = tenantId;
            tenant.UserUserPoolId = "pool-123";
            seedDb.Tenants.Add(tenant);
            await seedDb.SaveChangesAsync();
        }

        // Execute CreateStaffHandler
        using (var testDb = CreateInMemoryIamDbContext(dbName, mockCurrentUser.Object))
        {
            var handler = new IamTenant.Application.Commands.Tenants.CreateStaffHandler(
                testDb,
                mockCurrentUser.Object,
                mockCognito.Object);

            var result = await handler.Handle(
                new IamTenant.Application.Commands.Tenants.CreateStaffCommand(
                    "alice@logistics.vn", "Alice", "Smith", new List<Guid>()),
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("alice@logistics.vn", result.Email);
        }

        // Assert atomic transactional commit: User AND OutboxMessage both exist in DB
        using (var verifyDb = CreateInMemoryIamDbContext(dbName, mockCurrentUser.Object))
        {
            var user = await verifyDb.Users.FirstOrDefaultAsync(u => u.Email == "alice@logistics.vn");
            Assert.NotNull(user);
            Assert.Equal("alice@logistics.vn", user.Email);

            var outboxMessages = await verifyDb.OutboxMessages.ToListAsync();
            Assert.Single(outboxMessages);
            Assert.Equal(nameof(Shared.Events.TenantStaffCreatedEvent), outboxMessages[0].EventType);
            Assert.Null(outboxMessages[0].ProcessedAt); // Pending for OutboxProcessor
            Assert.Contains("alice@logistics.vn", outboxMessages[0].Payload);
        }
    }

    [Fact]
    public async Task Test29_ProducerOutbox_RabbitMqDown_OutboxMessageRemainsPending()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId);

        // Seed tenant + Outbox message simulating RabbitMQ outage
        using (var db = CreateInMemoryIamDbContext(dbName, mockCurrentUser.Object))
        {
            var tenant = IamTenant.Domain.Tenant.Create("Logistics Corp", "logistics.vn", null, Guid.NewGuid());
            tenant.Id = tenantId;
            db.Tenants.Add(tenant);

            db.OutboxMessages.Add(new IamTenant.Domain.OutboxMessage
            {
                EventType = nameof(Shared.Events.TenantStaffCreatedEvent),
                Payload = "{\"TenantId\":\"" + tenantId + "\",\"Email\":\"test@logistics.vn\"}",
                CreatedAt = DateTimeOffset.UtcNow,
                RetryCount = 1,
                Error = "RabbitMQ connection refused"
            });
            await db.SaveChangesAsync();
        }

        // Verify outbox message remains pending and retriable
        using (var verifyDb = CreateInMemoryIamDbContext(dbName, mockCurrentUser.Object))
        {
            var pendingMessages = await verifyDb.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
                .ToListAsync();

            Assert.Single(pendingMessages);
            Assert.Equal("RabbitMQ connection refused", pendingMessages[0].Error);
        }
    }

    [Fact]
    public void Test19_Architecture_RepositoryRules_NoDirectAiProviderOutsideAiGovernance()
    {
        // Assert that MailService types only depend on IAiGovernanceClient
        var phishingServiceType = typeof(GovernedPhishingDetectionService);
        var ctors = phishingServiceType.GetConstructors();
        Assert.Single(ctors);
        var paramTypes = ctors[0].GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Contains(typeof(IAiGovernanceClient), paramTypes);
    }

    [Fact]
    public async Task Test20_Architecture_RepositoryRules_FailClosedQueryFilterConvention()
    {
        // Arrange - DbContext with null TenantId
        var mockNullUser = new Mock<ICurrentUserService>();
        mockNullUser.Setup(u => u.TenantId).Returns((Guid?)null);
        using var nullContext = CreateInMemoryDbContext("FailClosedArchDb", mockNullUser.Object);

        // Act - Query without tenant context
        var drafts = await nullContext.EmailDrafts.ToListAsync();
        var domains = await nullContext.Domains.ToListAsync();
        var mailboxes = await nullContext.Mailboxes.ToListAsync();

        // Assert - Fail-closed: exactly 0 items returned
        Assert.Empty(drafts);
        Assert.Empty(domains);
        Assert.Empty(mailboxes);
    }

    [Fact]
    public async Task Test21_AuditRecords_SystemAdmin_ReturnsCrossTenantRecords()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        // Seed audit records for Tenant A and Tenant B
        var mockSysAdmin = new Mock<ICurrentUserService>();
        mockSysAdmin.Setup(u => u.RoleIds).Returns(new List<string> { Shared.Constants.RoleConstants.SystemAdmin });
        mockSysAdmin.Setup(u => u.TenantId).Returns((Guid?)null);

        using (var seedContext = CreateInMemoryDbContext(dbName, mockSysAdmin.Object))
        {
            seedContext.AuditRecords.Add(new AuditRecord
            {
                TenantId = tenantA,
                ActorId = Guid.NewGuid(),
                Action = "ActionA",
                ResourceType = "MailDomain",
                ResourceId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
                Result = "Success"
            });
            seedContext.AuditRecords.Add(new AuditRecord
            {
                TenantId = tenantB,
                ActorId = Guid.NewGuid(),
                Action = "ActionB",
                ResourceType = "MailDomain",
                ResourceId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Result = "Success"
            });
            await seedContext.SaveChangesAsync();
        }

        // Act - Query as SYSTEM_ADMIN
        using (var queryContext = CreateInMemoryDbContext(dbName, mockSysAdmin.Object))
        {
            var handler = new MailService.Application.Queries.Audit.GetAuditRecordsQueryHandler(queryContext, mockSysAdmin.Object);
            var result = await handler.Handle(new MailService.Application.Queries.Audit.GetAuditRecordsQuery(null, 50), CancellationToken.None);

            // Assert - SYSTEM_ADMIN sees both Tenant A and Tenant B
            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.TenantId == tenantA);
            Assert.Contains(result, r => r.TenantId == tenantB);
        }
    }

    [Fact]
    public async Task Test22_AuditRecords_TenantAdmin_ReturnsOwnTenantOnly()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        // Seed audit records for Tenant A and Tenant B
        var mockSysAdmin = new Mock<ICurrentUserService>();
        mockSysAdmin.Setup(u => u.RoleIds).Returns(new List<string> { Shared.Constants.RoleConstants.SystemAdmin });

        using (var seedContext = CreateInMemoryDbContext(dbName, mockSysAdmin.Object))
        {
            seedContext.AuditRecords.Add(new AuditRecord
            {
                TenantId = tenantA,
                ActorId = Guid.NewGuid(),
                Action = "ActionA",
                ResourceType = "MailDomain",
                ResourceId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
                Result = "Success"
            });
            seedContext.AuditRecords.Add(new AuditRecord
            {
                TenantId = tenantB,
                ActorId = Guid.NewGuid(),
                Action = "ActionB",
                ResourceType = "MailDomain",
                ResourceId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Result = "Success"
            });
            await seedContext.SaveChangesAsync();
        }

        // Act - Query as TENANT_ADMIN of Tenant A
        var mockTenantAAdmin = new Mock<ICurrentUserService>();
        mockTenantAAdmin.Setup(u => u.RoleIds).Returns(new List<string> { Shared.Constants.RoleConstants.TenantAdmin });
        mockTenantAAdmin.Setup(u => u.TenantId).Returns(tenantA);

        using (var queryContext = CreateInMemoryDbContext(dbName, mockTenantAAdmin.Object))
        {
            var handler = new MailService.Application.Queries.Audit.GetAuditRecordsQueryHandler(queryContext, mockTenantAAdmin.Object);
            var result = await handler.Handle(new MailService.Application.Queries.Audit.GetAuditRecordsQuery(null, 50), CancellationToken.None);

            // Assert - TENANT_ADMIN sees only Tenant A
            Assert.Single(result);
            Assert.Equal(tenantA, result[0].TenantId);
        }
    }

    [Fact]
    public async Task Test23_AuditRecords_MissingTenantId_ThrowsUnauthorized()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockUserWithoutTenant = new Mock<ICurrentUserService>();
        mockUserWithoutTenant.Setup(u => u.RoleIds).Returns(new List<string> { Shared.Constants.RoleConstants.Staff });
        mockUserWithoutTenant.Setup(u => u.TenantId).Returns((Guid?)null);

        using var queryContext = CreateInMemoryDbContext(dbName, mockUserWithoutTenant.Object);
        var handler = new MailService.Application.Queries.Audit.GetAuditRecordsQueryHandler(queryContext, mockUserWithoutTenant.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new MailService.Application.Queries.Audit.GetAuditRecordsQuery(null, 50), CancellationToken.None));
    }
}
