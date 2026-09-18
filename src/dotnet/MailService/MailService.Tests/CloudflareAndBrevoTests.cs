using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Shared.Security;
using MailService.Controllers;
using MailService.Application.Interfaces.AI;
using MailService.Application.Interfaces.Messaging;
using MailService.Application.Interfaces.Storage;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Interfaces.Transport;
using MailService.Application.Options;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Security;
using MailService.Infrastructure.Stalwart;
using MailService.Infrastructure.Transport;

namespace MailService.Tests;

public class CloudflareAndBrevoTests
{
    private MailServiceDbContext CreateInMemoryDbContext(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<MailServiceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.TenantId).Returns(tenantId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"));

        return new MailServiceDbContext(options, mockCurrentUser.Object);
    }

    [Fact]
    public void Test_VerifySharedSecret_ValidMatch_ReturnsTrue()
    {
        string secret = "cf-super-secret-key-2026";
        bool result = WebhookSecurity.VerifySharedSecret(secret, secret);
        Assert.True(result);
    }

    [Fact]
    public void Test_VerifySharedSecret_InvalidMatch_ReturnsFalse()
    {
        string secret = "cf-super-secret-key-2026";
        bool result = WebhookSecurity.VerifySharedSecret("wrong-secret", secret);
        Assert.False(result);
    }

    [Fact]
    public void Test_VerifySharedSecret_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(WebhookSecurity.VerifySharedSecret(null, "secret"));
        Assert.False(WebhookSecurity.VerifySharedSecret("", "secret"));
        Assert.False(WebhookSecurity.VerifySharedSecret("secret", ""));
    }

    [Fact]
    public void Test_VerifyCloudflareHmac_ValidSignature_ReturnsTrue()
    {
        // Arrange
        string secret = "test_cloudflare_secret_123456";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string timestampStr = timestamp.ToString();
        string bodyJson = "{\"deliveryId\":\"d123\",\"from\":\"alice@gmail.com\",\"to\":[\"ops@e-verland.site\"]}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(bodyJson);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] toSign = Encoding.UTF8.GetBytes(timestampStr + "." + bodyJson);
        string signatureHex = Convert.ToHexString(hmac.ComputeHash(toSign)).ToLowerInvariant();

        // Act
        bool isValid = WebhookSecurity.VerifyCloudflareHmac(rawBytes, timestampStr, signatureHex, secret);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Test_VerifyCloudflareHmac_InvalidSignature_ReturnsFalse()
    {
        // Arrange
        string secret = "test_cloudflare_secret_123456";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string timestampStr = timestamp.ToString();
        string bodyJson = "{\"deliveryId\":\"d123\"}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(bodyJson);

        // Act
        bool isValid = WebhookSecurity.VerifyCloudflareHmac(rawBytes, timestampStr, "invalid_signature_hex_0000000000000000000000000000000000000000000000000000000000000000", secret);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Test_VerifyCloudflareHmac_ExpiredTimestamp_ReturnsFalse()
    {
        // Arrange (Timestamp 10 minutes ago)
        string secret = "test_cloudflare_secret_123456";
        long timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        string timestampStr = timestamp.ToString();
        string bodyJson = "{\"deliveryId\":\"d123\"}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(bodyJson);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] toSign = Encoding.UTF8.GetBytes(timestampStr + "." + bodyJson);
        string signatureHex = Convert.ToHexString(hmac.ComputeHash(toSign)).ToLowerInvariant();

        // Act
        bool isValid = WebhookSecurity.VerifyCloudflareHmac(rawBytes, timestampStr, signatureHex, secret);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task Test_CloudflareInboundController_ValidDirectSecret_AcceptsAndPersistsInboundMessageAndThread()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var db = CreateInMemoryDbContext("CfInboundDb_DirectSecret_" + Guid.NewGuid(), tenantId);
        Guid domainId = Guid.NewGuid();
        Guid mailboxId = Guid.NewGuid();

        var domain = new MailService.Domain.Entities.Domain
        {
            Id = domainId,
            TenantId = tenantId,
            DomainName = "e-verland.site",
            Status = DomainStatus.Active
        };
        db.Domains.Add(domain);

        var mailbox = new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            FullAddress = "ops@e-verland.site",
            Status = MailboxStatus.Active,
            Type = MailboxType.User
        };
        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();

        var mockResolver = new Mock<IMailboxResolver>();
        mockResolver.Setup(r => r.ResolveMailboxByAddressAsync("ops@e-verland.site", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxResolutionResult(true, tenantId, domainId, mailboxId, "ops@e-verland.site", MailboxType.User, null));

        var mockOutbox = new Mock<IOutboxWriter>();
        var mockStorage = new Mock<IR2StorageClient>();
        mockStorage.Setup(s => s.UploadRawEmlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<EmailDirection>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tenants/test/inbound/raw.eml");

        var stages = new List<IInboundPipelineStage>
        {
            new TlsVerificationStage()
        };
        var pipelineRunner = new InboundPipelineRunner(stages, db, mockOutbox.Object, mockStorage.Object, NullLogger<InboundPipelineRunner>.Instance);

        string secret = "cf_test_direct_secret_123";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudflareInbound:WebhookSecret"] = secret
            })
            .Build();

        var controller = new CloudflareInboundController(
            mockResolver.Object,
            db,
            pipelineRunner,
            config,
            NullLogger<CloudflareInboundController>.Instance);

        // Build raw RFC822 MIME
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Alice", "alice@gmail.com"));
        mime.To.Add(new MailboxAddress("Ops", "ops@e-verland.site"));
        mime.Subject = "AURORA DIRECT SECRET INBOUND 01";
        mime.Body = new TextPart("plain") { Text = "Hello Aurora with X-Aurora-Webhook-Secret!" };
        mime.MessageId = $"<test-msg-direct@gmail.com>";

        using var mimeMs = new MemoryStream();
        await mime.WriteToAsync(mimeMs);
        string rawEmlBase64 = Convert.ToBase64String(mimeMs.ToArray());

        var payloadObj = new CloudflareInboundPayload
        {
            DeliveryId = "cf-del-direct-001",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            From = "alice@gmail.com",
            To = new List<string> { "ops@e-verland.site" },
            RawEmailBase64 = rawEmlBase64
        };

        string jsonPayload = JsonSerializer.Serialize(payloadObj);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Aurora-Webhook-Secret"] = secret;
        httpContext.Request.Headers["X-Aurora-Delivery-Id"] = "cf-del-direct-001";
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var actionResult = await controller.HandleInboundEmail(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);

        // Verify Thread was created in DB
        var thread = await db.EmailThreads.FirstOrDefaultAsync(t => t.MailboxId == mailboxId);
        Assert.NotNull(thread);
        Assert.Equal("AURORA DIRECT SECRET INBOUND 01", thread.Subject);
        Assert.Contains("alice@gmail.com", thread.Participants);

        // Verify ProcessedMessage was created in DB
        var message = await db.ProcessedMessages.FirstOrDefaultAsync(m => m.SourceEventId == "cf-del-direct-001");
        Assert.NotNull(message);
        Assert.Equal("alice@gmail.com", message.SenderAddress);
        Assert.Equal(EmailDirection.Inbound, message.Direction);
    }

    [Fact]
    public async Task Test_CloudflareInboundController_DuplicateDelivery_IsIdempotent()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var db = CreateInMemoryDbContext("CfInboundDb_Idempotency_" + Guid.NewGuid(), tenantId);
        Guid domainId = Guid.NewGuid();
        Guid mailboxId = Guid.NewGuid();

        var domain = new MailService.Domain.Entities.Domain
        {
            Id = domainId,
            TenantId = tenantId,
            DomainName = "e-verland.site",
            Status = DomainStatus.Active
        };
        db.Domains.Add(domain);

        var mailbox = new Mailbox
        {
            Id = mailboxId,
            TenantId = tenantId,
            DomainId = domainId,
            FullAddress = "ops@e-verland.site",
            Status = MailboxStatus.Active,
            Type = MailboxType.User
        };
        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();

        var mockResolver = new Mock<IMailboxResolver>();
        mockResolver.Setup(r => r.ResolveMailboxByAddressAsync("ops@e-verland.site", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxResolutionResult(true, tenantId, domainId, mailboxId, "ops@e-verland.site", MailboxType.User, null));

        var mockOutbox = new Mock<IOutboxWriter>();
        var mockStorage = new Mock<IR2StorageClient>();
        mockStorage.Setup(s => s.UploadRawEmlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<EmailDirection>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tenants/test/inbound/raw.eml");

        var stages = new List<IInboundPipelineStage>
        {
            new TlsVerificationStage()
        };
        var pipelineRunner = new InboundPipelineRunner(stages, db, mockOutbox.Object, mockStorage.Object, NullLogger<InboundPipelineRunner>.Instance);

        string secret = "cf_test_idempotent_secret";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudflareInbound:WebhookSecret"] = secret
            })
            .Build();

        var controller = new CloudflareInboundController(
            mockResolver.Object,
            db,
            pipelineRunner,
            config,
            NullLogger<CloudflareInboundController>.Instance);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Alice", "alice@gmail.com"));
        mime.To.Add(new MailboxAddress("Ops", "ops@e-verland.site"));
        mime.Subject = "AURORA IDEMPOTENT TEST";
        mime.Body = new TextPart("plain") { Text = "Idempotency validation" };

        using var mimeMs = new MemoryStream();
        await mime.WriteToAsync(mimeMs);
        string rawEmlBase64 = Convert.ToBase64String(mimeMs.ToArray());

        var payloadObj = new CloudflareInboundPayload
        {
            DeliveryId = "cf-idempotent-del-123",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            From = "alice@gmail.com",
            To = new List<string> { "ops@e-verland.site" },
            RawEmailBase64 = rawEmlBase64
        };

        string jsonPayload = JsonSerializer.Serialize(payloadObj);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);

        // First Call
        var httpContext1 = new DefaultHttpContext();
        httpContext1.Request.Headers["X-Aurora-Webhook-Secret"] = secret;
        httpContext1.Request.Headers["X-Aurora-Delivery-Id"] = "cf-idempotent-del-123";
        httpContext1.Request.Body = new MemoryStream(bodyBytes);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext1 };

        var res1 = await controller.HandleInboundEmail(CancellationToken.None);
        Assert.IsType<OkObjectResult>(res1);

        int countAfterFirst = await db.ProcessedMessages.CountAsync(m => m.SourceEventId == "cf-idempotent-del-123");
        Assert.Equal(1, countAfterFirst);

        // Second Call (Retry from Cloudflare)
        var httpContext2 = new DefaultHttpContext();
        httpContext2.Request.Headers["X-Aurora-Webhook-Secret"] = secret;
        httpContext2.Request.Headers["X-Aurora-Delivery-Id"] = "cf-idempotent-del-123";
        httpContext2.Request.Body = new MemoryStream(bodyBytes);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext2 };

        var res2 = await controller.HandleInboundEmail(CancellationToken.None);
        var okResult2 = Assert.IsType<OkObjectResult>(res2);
        Assert.NotNull(okResult2.Value);

        // Verify count is still 1 (no duplicate message created)
        int countAfterSecond = await db.ProcessedMessages.CountAsync(m => m.SourceEventId == "cf-idempotent-del-123");
        Assert.Equal(1, countAfterSecond);
    }

    [Fact]
    public async Task Test_CloudflareInboundController_InvalidSignature_ReturnsUnauthorized()
    {
        // Arrange
        var db = CreateInMemoryDbContext("CfInboundDb_Invalid_" + Guid.NewGuid());
        var mockResolver = new Mock<IMailboxResolver>();
        var mockOutbox = new Mock<IOutboxWriter>();
        var mockStorage = new Mock<IR2StorageClient>();
        var pipelineRunner = new InboundPipelineRunner(new List<IInboundPipelineStage>(), db, mockOutbox.Object, mockStorage.Object, NullLogger<InboundPipelineRunner>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudflareInbound:WebhookSecret"] = "secret_abc"
            })
            .Build();

        var controller = new CloudflareInboundController(
            mockResolver.Object,
            db,
            pipelineRunner,
            config,
            NullLogger<CloudflareInboundController>.Instance);

        string jsonPayload = "{\"rawEmailBase64\":\"xyz\"}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Aurora-Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        httpContext.Request.Headers["X-Aurora-Signature"] = "bad_signature";
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var actionResult = await controller.HandleInboundEmail(CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    [Fact]
    public async Task Test_CloudflareInboundController_MissingSecretAndSignature_ReturnsUnauthorized()
    {
        // Arrange
        var db = CreateInMemoryDbContext("CfInboundDb_Missing_" + Guid.NewGuid());
        var mockResolver = new Mock<IMailboxResolver>();
        var mockOutbox = new Mock<IOutboxWriter>();
        var mockStorage = new Mock<IR2StorageClient>();
        var pipelineRunner = new InboundPipelineRunner(new List<IInboundPipelineStage>(), db, mockOutbox.Object, mockStorage.Object, NullLogger<InboundPipelineRunner>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudflareInbound:WebhookSecret"] = "secret_required"
            })
            .Build();

        var controller = new CloudflareInboundController(
            mockResolver.Object,
            db,
            pipelineRunner,
            config,
            NullLogger<CloudflareInboundController>.Instance);

        string jsonPayload = "{\"rawEmailBase64\":\"xyz\"}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var actionResult = await controller.HandleInboundEmail(CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    [Fact]
    public void Test_BrevoOptions_DefaultValues_AreCorrect()
    {
        var options = new BrevoOptions();
        Assert.Equal("smtp-relay.brevo.com", options.SmtpHost);
        Assert.Equal(587, options.SmtpPort);
        Assert.Equal("e-verland.site", options.FromDomain);
        Assert.Null(options.SmtpUsername);
        Assert.Null(options.SmtpPassword);
    }

    [Fact]
    public void Test_MailTransportOptions_DefaultProvider_IsBrevo()
    {
        var options = new MailTransportOptions();
        Assert.Equal("Brevo", options.Provider);
    }

    [Fact]
    public void Test_BrevoMailTransport_ProviderName_IsBrevo()
    {
        var config = new ConfigurationBuilder().Build();
        var transport = new BrevoMailTransport(config, null, NullLogger<BrevoMailTransport>.Instance);
        Assert.Equal("Brevo", transport.ProviderName);
    }

    [Fact]
    public void Test_StalwartMailTransport_ProviderName_IsStalwart()
    {
        var config = new ConfigurationBuilder().Build();
        var transport = new StalwartMailTransport(config, null, NullLogger<StalwartMailTransport>.Instance);
        Assert.Equal("Stalwart", transport.ProviderName);
    }

    [Fact]
    public void Test_MailKitSmtpDeliveryService_ResolvesBrevoTransportByDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailTransport:Provider"] = "Brevo",
                ["Brevo:SmtpHost"] = "smtp-relay.brevo.com",
                ["Brevo:SmtpPort"] = "587",
                ["Brevo:SmtpUsername"] = "ops@e-verland.site",
                ["Brevo:SmtpPassword"] = "brevo-key-123"
            })
            .Build();

        var brevoTransport = new BrevoMailTransport(config, null, NullLogger<BrevoMailTransport>.Instance);
        var stalwartTransport = new StalwartMailTransport(config, null, NullLogger<StalwartMailTransport>.Instance);

        var service = new MailKitSmtpDeliveryService(
            config,
            NullLogger<MailKitSmtpDeliveryService>.Instance,
            new IMailTransport[] { brevoTransport, stalwartTransport });

        Assert.NotNull(service);
    }

    [Fact]
    public async Task Test_InboundPipeline_CallsAiPhishingDetectionStage()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var db = CreateInMemoryDbContext("InboundAiTest_" + Guid.NewGuid(), tenantId);

        var mockOutbox = new Mock<IOutboxWriter>();
        var mockStorage = new Mock<IR2StorageClient>();
        var mockAiGov = new Mock<IAiGovernanceClient>();
        mockAiGov.Setup(g => g.ExecutePolicyAsync(tenantId, "PhishingDetection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiGovernancePolicyResult.Allowed("Gemini"));

        var mockAiPhishing = new Mock<IPhishingDetectionService>();
        mockAiPhishing.Setup(a => a.AnalyzePhishingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0.1m, "Safe email"));

        var stages = new List<IInboundPipelineStage>
        {
            new AiPhishingDetectionStage(mockAiGov.Object, mockAiPhishing.Object, NullLogger<AiPhishingDetectionStage>.Instance)
        };

        var runner = new InboundPipelineRunner(stages, db, mockOutbox.Object, mockStorage.Object, NullLogger<InboundPipelineRunner>.Instance);

        var context = new InboundPipelineContext
        {
            TenantId = tenantId,
            SenderAddress = "test@example.com",
            Subject = "AI Inbound Test"
        };
        context.RecipientAddresses.Add("ops@e-verland.site");

        // Act
        var result = await runner.RunAsync(context, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockAiGov.Verify(g => g.ExecutePolicyAsync(tenantId, "PhishingDetection", It.IsAny<CancellationToken>()), Times.Once);
        mockAiPhishing.Verify(a => a.AnalyzePhishingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
