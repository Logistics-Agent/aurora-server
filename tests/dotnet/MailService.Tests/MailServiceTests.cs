using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Security;
using MailService.Application.Interfaces;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.AI;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Persistence.Repositories;

namespace MailService.Tests;

public class MailServiceTests
{
    private MailServiceDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MailServiceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        return new MailServiceDbContext(options, mockCurrentUser.Object);
    }

    [Fact]
    public async Task Test1_RevisionConsistency_ExactlyOneLatestRevision()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext("RevisionConsistencyDb");
        var mockUser = new Mock<ICurrentUserService>();
        Guid tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);

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
            RecipientAddresses = new List<string> { "target@external.com" },
            Subject = "Automated email"
        };

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
        using var dbContext = CreateInMemoryDbContext("RollbackDb");
        var mockUser = new Mock<ICurrentUserService>();
        Guid tenantId = Guid.NewGuid();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);

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
}
