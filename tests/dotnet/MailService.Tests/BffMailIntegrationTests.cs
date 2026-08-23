using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using StaffBff.Controllers;
using AdminBff.Controllers;
using SystemBff.Controllers;
using BuildingBlocks.BFF.Mail.Clients;
using BuildingBlocks.BFF.Mail.Models;
using BuildingBlocks.BFF.Mail.Validation;
using Shared.Entity;
using Shared.Security;
using MailService.Domain.Entities;

namespace MailService.Tests;

public class BffMailIntegrationTests
{
    private readonly Mock<IMailServiceClient> _mockMailClient = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ILogger<MailController>> _mockStaffLogger = new();
    private readonly Mock<ILogger<MailAdminController>> _mockAdminLogger = new();
    private readonly Mock<ILogger<MailSystemController>> _mockSystemLogger = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public BffMailIntegrationTests()
    {
        _mockCurrentUser.Setup(u => u.TenantId).Returns(_tenantId);
        _mockCurrentUser.Setup(u => u.UserId).Returns(_userId);
    }

    private void SetupControllerContext(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // ─── 1. Staff BFF Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task StaffBff_CreateDraft_ValidRequest_ReturnsCreated()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        var draftId = Guid.NewGuid().ToString();
        var mailboxId = Guid.NewGuid().ToString();
        var request = new CreateDraftRequest(mailboxId, null, "Quotation follow up", "Please find attached the quotation.");

        var expectedDraft = new DraftResponse(
            draftId,
            Guid.NewGuid().ToString(),
            1,
            true,
            "Manual",
            "Draft",
            mailboxId,
            null,
            request.Subject,
            request.Body,
            "hash123",
            DateTimeOffset.UtcNow);

        _mockMailClient.Setup(c => c.CreateDraftAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDraft);

        var result = await controller.CreateDraft(request);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var response = Assert.IsType<DraftResponse>(createdResult.Value);
        Assert.Equal(draftId, response.DraftId);
    }

    [Fact]
    public async Task StaffBff_CreateDraft_InvalidRequest_ReturnsBadRequest()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        // MailboxId is not a valid GUID and subject is empty
        var invalidRequest = new CreateDraftRequest("not-a-guid", null, "", "Body");

        var result = await controller.CreateDraft(invalidRequest);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task StaffBff_SubmitOutboundMessage_Valid_ReturnsOk()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "client@example.com" },
            "Booking Confirmation",
            "Your shipment is confirmed.",
            "<p>Your shipment is confirmed.</p>");

        var expectedResponse = new SubmitOutboundMessageResponse(
            Guid.NewGuid().ToString(),
            "QUEUE-12345",
            DateTimeOffset.UtcNow);

        _mockMailClient.Setup(c => c.SubmitOutboundMessageAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.SubmitOutboundMessage(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<SubmitOutboundMessageResponse>(okResult.Value);
        Assert.Equal("QUEUE-12345", response.StalwartQueueId);
    }

    [Fact]
    public async Task StaffBff_SubmitOutboundMessage_ExceedsTotalAttachmentLimit_ReturnsBadRequest()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        // 3 attachments of 20MB each = 60MB total (exceeds 50MB MaxTotalAttachmentBytes)
        var largeBase64 = Convert.ToBase64String(new byte[20 * 1024 * 1024]);
        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "client@example.com" },
            "Heavy Attachments",
            "Body",
            "<p>Body</p>",
            new List<OutboundAttachmentDto>
            {
                new("file1.pdf", "application/pdf", largeBase64),
                new("file2.pdf", "application/pdf", largeBase64),
                new("file3.pdf", "application/pdf", largeBase64)
            });

        var result = await controller.SubmitOutboundMessage(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task StaffBff_ReleaseQuarantine_ReturnsOk()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        var quarantineId = Guid.NewGuid().ToString();
        var expectedResponse = new ReleaseQuarantineResponse(true, DateTimeOffset.UtcNow);

        _mockMailClient.Setup(c => c.ReleaseQuarantineAsync(quarantineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.ReleaseQuarantine(quarantineId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task StaffBff_GrpcNotFound_MapsToHttp404()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        var missingDraftId = Guid.NewGuid().ToString();
        _mockMailClient.Setup(c => c.GetDraftAsync(missingDraftId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RpcException(new Status(StatusCode.NotFound, $"Draft revision '{missingDraftId}' not found.")));

        var result = await controller.GetDraft(missingDraftId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task StaffBff_GrpcPermissionDenied_MapsToHttp403()
    {
        var controller = new MailController(_mockMailClient.Object, _mockCurrentUser.Object, _mockStaffLogger.Object);
        SetupControllerContext(controller);

        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "client@example.com" },
            "Restricted",
            "Body",
            "<p>Body</p>");

        _mockMailClient.Setup(c => c.SubmitOutboundMessageAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RpcException(new Status(StatusCode.PermissionDenied, "Outbound message rejected by security policy")));

        var result = await controller.SubmitOutboundMessage(request);

        var forbiddenResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbiddenResult.StatusCode);
    }

    // ─── 2. Admin BFF Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task AdminBff_ProvisionDomain_Valid_ReturnsCreated()
    {
        var controller = new MailAdminController(_mockMailClient.Object, _mockCurrentUser.Object, _mockAdminLogger.Object);
        SetupControllerContext(controller);

        var request = new ProvisionDomainRequest("aurora.vn", 100, 365);
        var expectedResponse = new ProvisionDomainResponse(
            Guid.NewGuid().ToString(),
            "aurora.vn",
            "aurora-2025",
            "v=DKIM1; k=rsa; p=...",
            DateTimeOffset.UtcNow);

        _mockMailClient.Setup(c => c.ProvisionDomainAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.ProvisionDomain(request);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var response = Assert.IsType<ProvisionDomainResponse>(createdResult.Value);
        Assert.Equal("aurora.vn", response.DomainName);
    }

    [Fact]
    public async Task AdminBff_ProvisionDomain_InvalidFqdn_ReturnsBadRequest()
    {
        var controller = new MailAdminController(_mockMailClient.Object, _mockCurrentUser.Object, _mockAdminLogger.Object);
        SetupControllerContext(controller);

        var invalidRequest = new ProvisionDomainRequest("invalid_domain_name!", -5, 0);

        var result = await controller.ProvisionDomain(invalidRequest);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task AdminBff_CreateMailbox_Valid_ReturnsCreated()
    {
        var controller = new MailAdminController(_mockMailClient.Object, _mockCurrentUser.Object, _mockAdminLogger.Object);
        SetupControllerContext(controller);

        var domainId = Guid.NewGuid().ToString();
        var request = new CreateMailboxRequest(domainId, "sales", null);
        var expectedResponse = new CreateMailboxResponse(
            Guid.NewGuid().ToString(),
            "sales@aurora.vn",
            DateTimeOffset.UtcNow);

        _mockMailClient.Setup(c => c.CreateMailboxAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.CreateMailbox(request);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var response = Assert.IsType<CreateMailboxResponse>(createdResult.Value);
        Assert.Equal("sales@aurora.vn", response.FullAddress);
    }

    [Fact]
    public async Task AdminBff_DeleteQuarantine_ReturnsOk()
    {
        var controller = new MailAdminController(_mockMailClient.Object, _mockCurrentUser.Object, _mockAdminLogger.Object);
        SetupControllerContext(controller);

        var quarantineId = Guid.NewGuid().ToString();
        _mockMailClient.Setup(c => c.DeleteQuarantineAsync(quarantineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteQuarantineResponse(true));

        var result = await controller.DeleteQuarantine(quarantineId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    // ─── 3. System BFF Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task SystemBff_RequeueDeadLetter_Valid_ReturnsOk()
    {
        var controller = new MailSystemController(_mockMailClient.Object, _mockCurrentUser.Object, _mockSystemLogger.Object);
        SetupControllerContext(controller);

        var messageId = Guid.NewGuid().ToString();
        var expectedResponse = new RequeueDeadLetterResponse(true, "Requeued successfully");

        _mockMailClient.Setup(c => c.RequeueDeadLetterAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.RequeueDeadLetter(messageId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task SystemBff_RequeueDeadLetter_InvalidGuid_ReturnsBadRequest()
    {
        var controller = new MailSystemController(_mockMailClient.Object, _mockCurrentUser.Object, _mockSystemLogger.Object);
        SetupControllerContext(controller);

        var result = await controller.RequeueDeadLetter("invalid-guid");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    // ─── 4. Architecture, Entity & Security Invariants ───────────────────────

    [Fact]
    public void ArchitectureRule_BaseEntityGeneratesVersion7IdByDefault()
    {
        // Verify all entities inheriting BaseEntity automatically receive non-empty Guid v7
        var domain = new Domain.Entities.Domain();
        var mailbox = new Mailbox();
        var alias = new Alias();
        var draft = new EmailDraft();
        var msg = new ProcessedMessage();
        var check = new SecurityCheckResult();
        var qRec = new QuarantineRecord();
        var outbox = new OutboxMessage();

        Assert.NotEqual(Guid.Empty, domain.Id);
        Assert.NotEqual(Guid.Empty, mailbox.Id);
        Assert.NotEqual(Guid.Empty, alias.Id);
        Assert.NotEqual(Guid.Empty, draft.Id);
        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.NotEqual(Guid.Empty, check.Id);
        Assert.NotEqual(Guid.Empty, qRec.Id);
        Assert.NotEqual(Guid.Empty, outbox.Id);

        // Version 7 check: 4 bits in version field = 7
        Assert.Equal(7, (domain.Id.ToByteArray()[7] >> 4));
        Assert.Equal(7, (draft.Id.ToByteArray()[7] >> 4));
    }

    [Fact]
    public void BffArchitectureRule_NoTenantIdInPublicRequestDtos()
    {
        // Assert that none of the client-submitted DTOs accept a TenantId in their request bodies
        var requestDtoTypes = new[]
        {
            typeof(ProvisionDomainRequest),
            typeof(CreateMailboxRequest),
            typeof(CreateAliasRequest),
            typeof(CreateDraftRequest),
            typeof(SubmitOutboundMessageRequest)
        };

        foreach (var type in requestDtoTypes)
        {
            var properties = type.GetProperties();
            Assert.DoesNotContain(properties, p => p.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void MailApiCatalog_DocumentationVerification_MatchesControllers()
    {
        // Robust discovery of repository root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "aurora-server.sln")) && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        var projectRoot = dir?.FullName ?? AppContext.BaseDirectory;
        var docPath = Path.Combine(projectRoot, "src", "dotnet", "MailService", "docs", "MAIL_API_CATALOG.md");

        Assert.True(File.Exists(docPath), $"Expected documentation at {docPath}");

        var content = File.ReadAllText(docPath);
        Assert.Contains("/api/v1/mail/drafts", content);
        Assert.Contains("/api/v1/mail/messages/outbound", content);
        Assert.Contains("/api/v1/mail/quarantine", content);
        Assert.Contains("/api/v1/admin/mail/domains", content);
        Assert.Contains("/api/v1/admin/mail/mailboxes", content);
        Assert.Contains("/api/v1/admin/mail/aliases", content);
        Assert.Contains("/api/v1/system/mail/dead-letter", content);
        Assert.Contains("200 OK", content);
        Assert.Contains("MaxTotalAttachmentBytes", content);
    }

    // ─── 5. Attachment & Payload Boundary Tests ─────────────────────────────

    [Fact]
    public async Task AttachmentBoundary_WithinLimits_PassesValidation()
    {
        var validator = new SubmitOutboundMessageRequestValidator();

        // 2 attachments of 10MB each = 20MB total (well within 50MB limit and 25MB single limit)
        var sample10MbBase64 = Convert.ToBase64String(new byte[10 * 1024 * 1024]);
        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "recipient@example.com" },
            "Valid Attachments",
            "Text body",
            "<p>HTML body</p>",
            new List<OutboundAttachmentDto>
            {
                new("report_part1.pdf", "application/pdf", sample10MbBase64),
                new("report_part2.pdf", "application/pdf", sample10MbBase64)
            });

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task AttachmentBoundary_SingleAttachmentExceeds25Mb_FailsValidation()
    {
        var validator = new SubmitOutboundMessageRequestValidator();

        // 1 attachment of 26MB (exceeds 25MB single limit)
        var sample26MbBase64 = Convert.ToBase64String(new byte[26 * 1024 * 1024]);
        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "recipient@example.com" },
            "Oversized Single Attachment",
            "Text body",
            string.Empty,
            new List<OutboundAttachmentDto>
            {
                new("heavy_data.zip", "application/zip", sample26MbBase64)
            });

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("25 MB") || e.ErrorMessage.Contains("25MB"));
    }

    [Fact]
    public async Task AttachmentBoundary_TotalAttachmentsExceed50Mb_FailsValidation()
    {
        var validator = new SubmitOutboundMessageRequestValidator();

        // 3 attachments of 18MB each = 54MB total (each < 25MB, but total > 50MB)
        var sample18MbBase64 = Convert.ToBase64String(new byte[18 * 1024 * 1024]);
        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "recipient@example.com" },
            "Oversized Total Attachments",
            "Text body",
            string.Empty,
            new List<OutboundAttachmentDto>
            {
                new("part1.pdf", "application/pdf", sample18MbBase64),
                new("part2.pdf", "application/pdf", sample18MbBase64),
                new("part3.pdf", "application/pdf", sample18MbBase64)
            });

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("50 MB"));
    }

    [Fact]
    public async Task AttachmentBoundary_Exceeds10AttachmentCount_FailsValidation()
    {
        var validator = new SubmitOutboundMessageRequestValidator();

        var smallBase64 = Convert.ToBase64String(new byte[1024]);
        var attachments = Enumerable.Range(1, 11)
            .Select(i => new OutboundAttachmentDto($"file_{i}.txt", "text/plain", smallBase64))
            .ToList();

        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "recipient@example.com" },
            "11 Attachments",
            "Text body",
            string.Empty,
            attachments);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Cannot attach more than 10 files"));
    }

    [Fact]
    public async Task AttachmentBoundary_InvalidBase64String_FailsValidation()
    {
        var validator = new SubmitOutboundMessageRequestValidator();

        var request = new SubmitOutboundMessageRequest(
            "sender@aurora.vn",
            new List<string> { "recipient@example.com" },
            "Corrupt Base64",
            "Text body",
            string.Empty,
            new List<OutboundAttachmentDto>
            {
                new("corrupt.pdf", "application/pdf", "This is not valid base64!@#$%^&*()")
            });

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PayloadHierarchy_Invariant_AttachmentTotalUnderGrpcAndHttpLimits()
    {
        // Enforce the architectural guarantee: Decoded total (50MB) < gRPC buffer (75MB) < HTTP body (80MB)
        Assert.True(MailLimits.MaxTotalAttachmentBytes < MailLimits.MaxGrpcMessageBytes,
            "MaxTotalAttachmentBytes must be less than MaxGrpcMessageBytes to avoid gRPC payload exhaustion.");

        Assert.True(MailLimits.MaxGrpcMessageBytes < MailLimits.MaxHttpRequestBodyBytes,
            "MaxGrpcMessageBytes must be less than MaxHttpRequestBodyBytes to allow HTTP framing & Base64 transfer.");

        Assert.Equal(10, MailLimits.MaxAttachmentCount);
        Assert.Equal(25 * 1024 * 1024, MailLimits.MaxSingleAttachmentBytes);
        Assert.Equal(50 * 1024 * 1024, MailLimits.MaxTotalAttachmentBytes);
        Assert.Equal(75 * 1024 * 1024, MailLimits.MaxGrpcMessageBytes);
        Assert.Equal(80 * 1024 * 1024, MailLimits.MaxHttpRequestBodyBytes);
    }
}
