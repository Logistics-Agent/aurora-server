using DocumentOcr.Grpc;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ShipmentWorkflow.Grpc;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class DocumentIntakeStatefulFakeTests
{
    [Fact]
    public async Task Replay_after_submit_failure_has_one_attachment_one_job_and_two_outbox_entries()
    {
        var fake = new StatefulIntakeFake { SubmitFailuresRemaining = 1 };
        var orchestrator = new DocumentIntakeOrchestrator(
            fake,
            fake,
            NullLogger<DocumentIntakeOrchestrator>.Instance);
        var request = new CreateDocumentIntakeRequestModel(
            fake.UploadId,
            "INVOICE",
            "stateful-intake-key");

        await Assert.ThrowsAsync<DocumentIntakeOrchestrationException>(() =>
            orchestrator.ComposeAsync(fake.ShipmentId, request, CancellationToken.None));
        var result = await orchestrator.ComposeAsync(
            fake.ShipmentId,
            request,
            CancellationToken.None);

        Assert.Equal(fake.IntakeId, result.IntakeId);
        Assert.Equal(fake.DocumentId, result.DocumentId);
        Assert.Equal(fake.JobId, result.OcrJobId);
        Assert.Equal(1, fake.AttachmentCount);
        Assert.Equal(1, fake.JobCount);
        Assert.Equal(2, fake.OutboxCount);
        Assert.Equal(
            [
                "ledger", "verify", "attach", "consume", "submit", "retry",
                "ledger", "verify", "attach", "consume", "submit", "submitted"
            ],
            fake.Operations);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.FailedPrecondition)]
    public async Task Retry_state_failure_preserves_original_orchestration_failure(StatusCode retryStatus)
    {
        var fake = new StatefulIntakeFake
        {
            SubmitFailuresRemaining = 1,
            RetryFailureStatus = retryStatus
        };
        var orchestrator = new DocumentIntakeOrchestrator(
            fake,
            fake,
            NullLogger<DocumentIntakeOrchestrator>.Instance);

        var exception = await Assert.ThrowsAsync<DocumentIntakeOrchestrationException>(() =>
            orchestrator.ComposeAsync(
                fake.ShipmentId,
                new CreateDocumentIntakeRequestModel(fake.UploadId, "INVOICE", "stateful-intake-key"),
                CancellationToken.None));

        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", exception.Code);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task Retry_state_cancellation_is_not_swallowed()
    {
        var fake = new StatefulIntakeFake
        {
            SubmitFailuresRemaining = 1,
            RetryFailureStatus = StatusCode.Cancelled
        };
        var orchestrator = new DocumentIntakeOrchestrator(
            fake,
            fake,
            NullLogger<DocumentIntakeOrchestrator>.Instance);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            orchestrator.ComposeAsync(
                fake.ShipmentId,
                new CreateDocumentIntakeRequestModel(fake.UploadId, "INVOICE", "stateful-intake-key"),
                CancellationToken.None));

        Assert.Equal(StatusCode.Cancelled, exception.StatusCode);
    }

    private sealed class StatefulIntakeFake :
        IDocumentIntakeOcrGateway,
        IShipmentDocumentIntakeGateway
    {
        public Guid ShipmentId { get; } = Guid.CreateVersion7();
        public Guid UploadId { get; } = Guid.CreateVersion7();
        public Guid IntakeId { get; } = Guid.CreateVersion7();
        public Guid DocumentId { get; } = Guid.CreateVersion7();
        public Guid JobId { get; } = Guid.CreateVersion7();
        public int SubmitFailuresRemaining { get; set; }
        public StatusCode? RetryFailureStatus { get; set; }
        public int AttachmentCount { get; private set; }
        public int JobCount { get; private set; }
        public int OutboxCount { get; private set; }
        public List<string> Operations { get; } = [];

        private string IntakeStatus { get; set; } = "PENDING_ATTACHMENT";
        private DocumentUploadStatus UploadStatus { get; set; } = DocumentUploadStatus.Uploaded;
        private bool HasJob { get; set; }

        public Task<DocumentIntakeResponse> CreateAsync(
            CreateDocumentIntakeRequest request,
            CancellationToken cancellationToken)
        {
            Operations.Add("ledger");
            return Task.FromResult(Response());
        }

        public Task<DocumentIntakeResponse> AttachAsync(
            AttachDocumentIntakeRequest request,
            CancellationToken cancellationToken)
        {
            Operations.Add("attach");
            Assert.Equal(UploadId.ToString(), request.UploadId);
            if (IntakeStatus == "PENDING_ATTACHMENT")
            {
                AttachmentCount++;
                OutboxCount++;
                IntakeStatus = "PENDING_OCR";
            }
            else if (IntakeStatus == "FAILED_RETRYABLE")
            {
                IntakeStatus = "PENDING_OCR";
            }
            return Task.FromResult(Response());
        }

        public Task<DocumentIntakeResponse> MarkRetryableAsync(
            MarkDocumentIntakeRetryableRequest request,
            CancellationToken cancellationToken)
        {
            Operations.Add("retry");
            if (RetryFailureStatus.HasValue)
                throw new RpcException(new Status(RetryFailureStatus.Value, "retry state write failed"));
            IntakeStatus = "FAILED_RETRYABLE";
            return Task.FromResult(Response());
        }

        public Task<DocumentIntakeResponse> MarkSubmittedAsync(
            MarkDocumentIntakeSubmittedRequest request,
            CancellationToken cancellationToken)
        {
            Operations.Add("submitted");
            IntakeStatus = "SUBMITTED";
            return Task.FromResult(Response());
        }

        public Task<DocumentUploadReceipt> VerifyAsync(Guid uploadId, CancellationToken cancellationToken)
        {
            Operations.Add("verify");
            return Task.FromResult(new DocumentUploadReceipt
            {
                UploadId = UploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                MimeType = "application/pdf",
                SizeBytes = 1_024,
                VerifiedMimeType = "application/pdf",
                VerifiedSizeBytes = 1_024,
                Status = UploadStatus
            });
        }

        public Task<DocumentUploadReceipt> ConsumeAsync(Guid uploadId, CancellationToken cancellationToken)
        {
            Operations.Add("consume");
            UploadStatus = DocumentUploadStatus.Consumed;
            return Task.FromResult(new DocumentUploadReceipt
            {
                UploadId = UploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = UploadStatus
            });
        }

        public Task<DocumentOcrJobResponse> SubmitAsync(
            SubmitOcrJobRequest request,
            CancellationToken cancellationToken)
        {
            Operations.Add("submit");
            if (SubmitFailuresRemaining > 0)
            {
                SubmitFailuresRemaining--;
                throw new RpcException(new Status(StatusCode.Unavailable, "OCR unavailable."));
            }

            if (!HasJob)
            {
                HasJob = true;
                JobCount++;
                OutboxCount++;
            }
            return Task.FromResult(new DocumentOcrJobResponse
            {
                JobId = JobId.ToString(),
                Status = DocumentOcrJobStatus.Queued,
                FileName = "invoice.pdf",
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            });
        }

        private DocumentIntakeResponse Response() => new()
        {
            IntakeId = IntakeId.ToString(),
            ShipmentId = ShipmentId.ToString(),
            DocumentId = DocumentId.ToString(),
            UploadId = UploadId.ToString(),
            StorageReference = "objects/tenant/upload/invoice.pdf",
            FileName = "invoice.pdf",
            Status = IntakeStatus,
            Stage = IntakeStatus
        };
    }
}
