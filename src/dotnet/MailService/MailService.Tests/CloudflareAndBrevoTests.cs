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
using MimeKit;
using Shared.Security;
using MailService.Controllers;
using MailService.Application.Interfaces.Messaging;
using MailService.Application.Interfaces.Storage;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Security;
using MailService.Infrastructure.Stalwart;

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
    public async Task Test_CloudflareInboundController_ValidPayload_AcceptsAndPersistsInboundMessageAndThread()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var db = CreateInMemoryDbContext("CfInboundDb_" + Guid.NewGuid(), tenantId);
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

        string secret = "cf_test_secret_123";
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
        mime.Subject = "AURORA CF INBOUND 01";
        mime.Body = new TextPart("plain") { Text = "Hello Aurora from Cloudflare Worker!" };
        mime.MessageId = $"<test-msg-001@gmail.com>";

        using var mimeMs = new MemoryStream();
        await mime.WriteToAsync(mimeMs);
        string rawEmlBase64 = Convert.ToBase64String(mimeMs.ToArray());

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payloadObj = new CloudflareInboundPayload
        {
            DeliveryId = "cf-del-999",
            Timestamp = timestamp,
            From = "alice@gmail.com",
            To = new List<string> { "ops@e-verland.site" },
            RawEmailBase64 = rawEmlBase64
        };

        string jsonPayload = JsonSerializer.Serialize(payloadObj);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] signInput = Encoding.UTF8.GetBytes(timestamp.ToString() + "." + jsonPayload);
        string signatureHex = Convert.ToHexString(hmac.ComputeHash(signInput)).ToLowerInvariant();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Aurora-Timestamp"] = timestamp.ToString();
        httpContext.Request.Headers["X-Aurora-Delivery-Id"] = "cf-del-999";
        httpContext.Request.Headers["X-Aurora-Signature"] = signatureHex;
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
        Assert.Equal("AURORA CF INBOUND 01", thread.Subject);
        Assert.Contains("alice@gmail.com", thread.Participants);

        // Verify ProcessedMessage was created in DB
        var message = await db.ProcessedMessages.FirstOrDefaultAsync(m => m.SourceEventId == "cf-del-999");
        Assert.NotNull(message);
        Assert.Equal("alice@gmail.com", message.SenderAddress);
        Assert.Equal(EmailDirection.Inbound, message.Direction);
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
    public void Test_MailKitSmtpDeliveryService_BrevoConfig_ResolvesCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailTransport:Provider"] = "Brevo",
                ["Brevo:SmtpHost"] = "smtp-relay.brevo.com",
                ["Brevo:SmtpPort"] = "587",
                ["Brevo:SmtpUsername"] = "ops@e-verland.site",
                ["Brevo:SmtpPassword"] = "xsmtpsib-secret-key"
            })
            .Build();

        // Act
        var service = new MailKitSmtpDeliveryService(config, NullLogger<MailKitSmtpDeliveryService>.Instance);

        // Assert
        Assert.NotNull(service);
    }
}
