using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MailService.Controllers;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Pipeline;
using MailService.Infrastructure.Messaging.Consumers;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Security;
using MailService.Infrastructure.Stalwart;
using Shared.Events;
using Shared.Security;
using Xunit;

namespace MailService.Tests;

public class StalwartIntegrationTests
{
    [Fact]
    public void WebhookSecurity_ValidBase64Hmac_ReturnsTrue()
    {
        string secret = "test-stalwart-secret-2026";
        byte[] payload = Encoding.UTF8.GetBytes("{\"events\":[{\"id\":\"evt_101\",\"type\":\"store.ingest\"}]}");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(payload);
        string base64Sig = Convert.ToBase64String(hash);

        bool isValid = WebhookSecurity.VerifyStalwartHmac(payload, base64Sig, secret);
        Assert.True(isValid);
    }

    [Fact]
    public void WebhookSecurity_InvalidBase64Hmac_ReturnsFalse()
    {
        string secret = "test-stalwart-secret-2026";
        byte[] payload = Encoding.UTF8.GetBytes("{\"events\":[]}");
        string wrongBase64Sig = Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong-signature"));

        bool isValid = WebhookSecurity.VerifyStalwartHmac(payload, wrongBase64Sig, secret);
        Assert.False(isValid);
    }

    [Fact]
    public async Task MailboxResolver_ByStalwartAccountId_ResolvesCorrectly()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var accountId = "acc_ops_stalwart_99";

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);

        using var db = CreateInMemoryDbContext(dbName, mockUser.Object);
        db.Domains.Add(new MailService.Domain.Entities.Domain
        {
            Id = domainId,
            TenantId = tenantId,
            DomainName = "e-verland.site",
            Status = DomainStatus.Active
        });
        db.Mailboxes.Add(new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainId = domainId,
            LocalPart = "ops",
            FullAddress = "ops@e-verland.site",
            StalwartAccountId = accountId,
            Status = MailboxStatus.Active,
            Type = MailboxType.Shared
        });
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new MailboxResolver(db, cache, NullLogger<MailboxResolver>.Instance);

        var result = await resolver.ResolveMailboxByStalwartAccountIdAsync(accountId);

        Assert.True(result.IsResolved);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("ops@e-verland.site", result.FullAddress);
        Assert.Equal(MailboxType.Shared, result.Type);
    }

    [Fact]
    public async Task MailboxResolver_ByRecipientAddress_DirectAndAlias_Work()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);

        using var db = CreateInMemoryDbContext(dbName, mockUser.Object);
        db.Domains.Add(new MailService.Domain.Entities.Domain
        {
            Id = domainId,
            TenantId = tenantId,
            DomainName = "e-verland.site",
            Status = DomainStatus.Active
        });
        db.Mailboxes.Add(new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainId = domainId,
            LocalPart = "support",
            FullAddress = "support@e-verland.site",
            Status = MailboxStatus.Active,
            Type = MailboxType.Shared
        });

        db.Aliases.Add(new Alias
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainId = domainId,
            AliasAddress = "helpdesk@e-verland.site",
            Targets = new List<string> { "support@e-verland.site" }
        });
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new MailboxResolver(db, cache, NullLogger<MailboxResolver>.Instance);

        // Direct resolve
        var directRes = await resolver.ResolveMailboxByAddressAsync("support@e-verland.site");
        Assert.True(directRes.IsResolved);
        Assert.Equal("support@e-verland.site", directRes.FullAddress);

        // Alias resolve
        var aliasRes = await resolver.ResolveMailboxByAddressAsync("helpdesk@e-verland.site");
        Assert.True(aliasRes.IsResolved);
        Assert.Equal("support@e-verland.site", aliasRes.FullAddress);
    }

    [Fact]
    public async Task StalwartWebhookController_ValidHmacAndRabbitMqPublish_ReturnsAccepted()
    {
        var secret = "webhook-sec";
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Stalwart:WebhookSecret"] = secret
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var mockPublish = new Mock<IPublishEndpoint>();
        mockPublish.Setup(p => p.Publish(It.IsAny<InboundEmailWebhookReceivedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new StalwartWebhookController(mockPublish.Object, config, NullLogger<StalwartWebhookController>.Instance);

        string rawPayload = "{\"events\":[{\"id\":\"evt_991\",\"createdAt\":\"2026-09-14T23:00:00Z\",\"type\":\"store.ingest\",\"data\":{\"accountName\":\"ops\",\"messageId\":\"msg_123\",\"to\":\"ops@e-verland.site\"}}]}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(rawPayload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        string base64Sig = Convert.ToBase64String(hmac.ComputeHash(rawBytes));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(rawBytes);
        httpContext.Request.Headers["X-Signature"] = base64Sig;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.HandleWebhook();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);

        mockPublish.Verify(p => p.Publish(It.Is<InboundEmailWebhookReceivedEvent>(e => e.StalwartEventId == "evt_991" && e.To == "ops@e-verland.site"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StalwartWebhookController_RabbitMqFailure_Returns503()
    {
        var secret = "webhook-sec";
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Stalwart:WebhookSecret"] = secret
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var mockPublish = new Mock<IPublishEndpoint>();
        mockPublish.Setup(p => p.Publish(It.IsAny<InboundEmailWebhookReceivedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ connection down"));

        var controller = new StalwartWebhookController(mockPublish.Object, config, NullLogger<StalwartWebhookController>.Instance);

        string rawPayload = "{\"events\":[{\"id\":\"evt_fail\",\"type\":\"store.ingest\",\"data\":{}}]}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(rawPayload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        string base64Sig = Convert.ToBase64String(hmac.ComputeHash(rawBytes));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(rawBytes);
        httpContext.Request.Headers["X-Signature"] = base64Sig;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.HandleWebhook();
        var statusResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }

    [Fact]
    public async Task InboundWebhookEventRepository_LockAcquisitionAndCompletion_Work()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockUser = new Mock<ICurrentUserService>();
        using var db = CreateInMemoryDbContext(dbName, mockUser.Object);

        var repo = new InboundWebhookEventRepository(db, NullLogger<InboundWebhookEventRepository>.Instance);

        // 1. Initial lock acquisition -> shouldProcess = true
        var (evt, shouldProcess) = await repo.AcquireEventLockAsync("evt_unique_1", "store.ingest", "{}");
        Assert.True(shouldProcess);
        Assert.Equal(WebhookEventStatus.Processing, evt.Status);

        // 2. Mark completed
        await repo.MarkCompletedAsync("evt_unique_1");

        // 3. Subsequent acquisition on completed event -> shouldProcess = false
        var (reacquired, reacquiredShouldProcess) = await repo.AcquireEventLockAsync("evt_unique_1", "store.ingest", "{}");
        Assert.False(reacquiredShouldProcess);
        Assert.Equal(WebhookEventStatus.Completed, reacquired.Status);
    }

    [Fact]
    public async Task InboundEmailWebhookConsumer_FullFlow_SucceedsAndMarksCompleted()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        using var db = CreateInMemoryDbContext(dbName, mockUser.Object);

        db.Mailboxes.Add(new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            LocalPart = "ops",
            FullAddress = "ops@e-verland.site",
            StalwartAccountId = "acc_ops_1",
            Status = MailboxStatus.Active
        });
        await db.SaveChangesAsync();

        var repo = new InboundWebhookEventRepository(db, NullLogger<InboundWebhookEventRepository>.Instance);
        var mockResolver = new Mock<IMailboxResolver>();
        mockResolver.Setup(r => r.ResolveMailboxByStalwartAccountIdAsync("acc_ops_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxResolutionResult(true, tenantId, domainId, mailboxId, "ops@e-verland.site", MailboxType.Shared, null));

        var mockJmap = new Mock<IStalwartJmapClient>();
        mockJmap.Setup(j => j.FetchEmailByWebhookDataAsync(It.IsAny<MailboxResolutionResult>(), It.IsAny<InboundEmailWebhookReceivedEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JmapEmailDto
            {
                Id = "msg_jmap_101",
                Subject = "Urgent Cargo Clearance",
                From = "customer@external.com",
                To = new List<string> { "ops@e-verland.site" },
                BodyText = "Please clear container B4."
            });

        var mockOutbox = new Mock<MailService.Application.Interfaces.Messaging.IOutboxWriter>();
        var mockStorage = new Mock<MailService.Application.Interfaces.Storage.IR2StorageClient>();
        var stages = new List<MailService.Application.Pipeline.IInboundPipelineStage>();
        var pipelineRunner = new InboundPipelineRunner(stages, db, mockOutbox.Object, mockStorage.Object, NullLogger<InboundPipelineRunner>.Instance);

        var consumer = new InboundEmailWebhookConsumer(db, repo, mockResolver.Object, mockJmap.Object, pipelineRunner, NullLogger<InboundEmailWebhookConsumer>.Instance);

        var mockContext = new Mock<ConsumeContext<InboundEmailWebhookReceivedEvent>>();
        mockContext.Setup(c => c.Message).Returns(new InboundEmailWebhookReceivedEvent
        {
            StalwartEventId = "evt_end_to_end_1",
            EventType = "store.ingest",
            AccountId = "acc_ops_1",
            To = "ops@e-verland.site",
            MessageId = "msg_jmap_101"
        });
        mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        // Verify Event is marked Completed
        var evt = await db.InboundWebhookEvents.FirstOrDefaultAsync(e => e.StalwartEventId == "evt_end_to_end_1");
        Assert.NotNull(evt);
        Assert.Equal(WebhookEventStatus.Completed, evt.Status);

        // Verify Thread was created
        var thread = await db.EmailThreads.FirstOrDefaultAsync(t => t.MailboxId == mailboxId);
        Assert.NotNull(thread);
        Assert.Equal("Urgent Cargo Clearance", thread.Subject);
    }

    private static MailServiceDbContext CreateInMemoryDbContext(string dbName, ICurrentUserService currentUserService)
    {
        var options = new DbContextOptionsBuilder<MailServiceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MailServiceDbContext(options, currentUserService);
    }
}

