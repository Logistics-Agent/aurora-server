using DocumentOcr.Contracts.Events;
using DocumentOcr.Application.Jobs;
using DocumentOcr.Grpc;
using Google.Protobuf.Reflection;
using System.Text.Json;
using DocumentOcr.Infrastructure.BackgroundJobs;

namespace DocumentOcr.Tests;

public sealed class DocumentOcrContractTests
{
    [Fact]
    public void SubmitRequestDoesNotExposeTenantOrUnsafeLocationFields()
    {
        var fields = SubmitDocumentJobRequest.Descriptor.Fields.InDeclarationOrder()
            .Select(field => field.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("tenant_id", fields);
        Assert.DoesNotContain("url", fields);
        Assert.DoesNotContain("local_path", fields);
        Assert.DoesNotContain("callback_url", fields);
        Assert.Contains("storage_reference", fields);
    }

    [Fact]
    public void ServiceExposesOnlyApprovedJobOperations()
    {
        var methods = DocumentOcrService.Descriptor.Methods
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(
            ["SubmitDocumentJob", "SubmitOcrJob", "GetDocumentJob", "ListDocumentJobs", "CancelDocumentJob", "RetryDocumentJob", "ReviewDocumentJob", "CreateUploadSession", "VerifyUploadSession", "GetUploadSession", "ConsumeUploadSession"],
            methods);
    }

    [Fact]
    public void UploadReceiptCarriesWriteTargetAndLifecycleMetadata()
    {
        var fields = DocumentUploadReceipt.Descriptor.Fields.InDeclarationOrder()
            .Select(field => field.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("storage_reference", fields);
        Assert.Contains("write_url", fields);
        Assert.Contains("required_headers", fields);
        Assert.Contains("expires_at", fields);
        Assert.Contains("maximum_size_bytes", fields);
        Assert.Contains("verified_content_sha256", fields);
        Assert.Equal("google.protobuf.Timestamp",
            DocumentUploadReceipt.Descriptor.FindFieldByName("expires_at")!.MessageType.FullName);
    }

    [Fact]
    public void JobResponseUsesTimestampForLifecycleDates()
    {
        var timestampFields = DocumentOcrJobResponse.Descriptor.Fields.InDeclarationOrder()
            .Where(field => field.FieldType == FieldType.Message)
            .Select(field => field.MessageType.FullName)
            .ToArray();

        Assert.NotEmpty(timestampFields);
        Assert.All(timestampFields, typeName => Assert.Equal("google.protobuf.Timestamp", typeName));
    }

    [Fact]
    public void IntegrationEventsUseApprovedContractVersionsAndUniqueIds()
    {
        var completed = new DocumentOcrCompletedEvent();
        var failed = new DocumentOcrFailedEvent();

        Assert.Equal(2, completed.ContractVersion);
        Assert.Equal(2, failed.ContractVersion);
        Assert.Equal(1, new DocumentOcrRequiresReviewEvent().ContractVersion);
        Assert.NotEqual(Guid.Empty, completed.EventId);
        Assert.NotEqual(Guid.Empty, failed.EventId);
        Assert.NotEqual(completed.EventId, failed.EventId);
    }

    [Fact]
    public void Version_two_events_preserve_legacy_fields_and_authoritative_linkage()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();
        var correlationId = Guid.CreateVersion7();

        var completed = DocumentOcrOutboxFactory.Completed(new DocumentOcrEventInput(
            tenantId, shipmentId, documentId, jobId, correlationId));
        var failed = DocumentOcrOutboxFactory.Failed(new DocumentOcrEventInput(
            tenantId, shipmentId, documentId, jobId, correlationId));
        var requiresReview = DocumentOcrOutboxFactory.RequiresReview(new DocumentOcrEventInput(
            tenantId, shipmentId, documentId, jobId, correlationId));

        var completedEvent = JsonSerializer.Deserialize<DocumentOcrCompletedEvent>(completed.Content)!;
        var failedEvent = JsonSerializer.Deserialize<DocumentOcrFailedEvent>(failed.Content)!;
        var reviewEvent = JsonSerializer.Deserialize<DocumentOcrRequiresReviewEvent>(requiresReview.Content)!;

        Assert.Equal(2, completedEvent.ContractVersion);
        Assert.Equal(2, failedEvent.ContractVersion);
        Assert.Equal(1, reviewEvent.ContractVersion);
        Assert.Equal(shipmentId, completedEvent.ShipmentId);
        Assert.Equal(documentId, completedEvent.DocumentId);
        Assert.Equal(jobId, failedEvent.JobId);
        Assert.Equal(correlationId, reviewEvent.CorrelationId);
        Assert.Equal(nameof(DocumentOcrCompletedEvent), completed.EventType);
        Assert.Equal(nameof(DocumentOcrFailedEvent), failed.EventType);
        Assert.Equal(nameof(DocumentOcrRequiresReviewEvent), requiresReview.EventType);
        Assert.True(DocumentOcrIntegrationEventTypeRegistry.TryResolve(
            nameof(DocumentOcrRequiresReviewEvent), out var eventType));
        Assert.Equal(typeof(DocumentOcrRequiresReviewEvent), eventType);
    }

}
