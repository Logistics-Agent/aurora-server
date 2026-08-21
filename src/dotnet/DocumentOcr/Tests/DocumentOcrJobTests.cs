using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;

namespace DocumentOcr.Tests;

public sealed class DocumentOcrJobTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid DocumentId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateInitializesQueuedTenantOwnedJob()
    {
        var job = CreateJob();

        Assert.Equal(TenantId, job.TenantId);
        Assert.Equal(DocumentId, job.ExternalDocumentId);
        Assert.Equal(DocumentOcrJobStatus.Queued, job.Status);
        Assert.Equal("request-001", job.IdempotencyKey);
        Assert.Empty(job.Attempts);
    }

    [Fact]
    public void CreateRejectsMissingTenantAndInvalidMetadata()
    {
        Assert.Throws<ArgumentException>(() => CreateJob(tenantId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateJob(storageReference: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateJob(sizeBytes: 0));
        Assert.Throws<ArgumentException>(() => CreateJob(externalDocumentId: Guid.Empty));
    }

    [Fact]
    public void StartCreatesProviderAttemptAndLease()
    {
        var job = CreateJob();

        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        Assert.Equal(DocumentOcrJobStatus.Processing, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(Now.AddMinutes(2), job.LeaseExpiresAt);
        Assert.Equal(OcrAttemptOutcome.Processing, attempt.Outcome);
        Assert.Same(attempt, Assert.Single(job.Attempts));
    }

    [Fact]
    public void CompleteStoresNormalizedResultAndClosesAttempt()
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        job.Complete(
            attempt.Id,
            OcrDocumentType.CommercialInvoice,
            "{\"invoiceNumber\":\"INV-1\"}",
            "{\"invoiceNumber\":0.98}",
            0.98m,
            false,
            "provider-request-1",
            Now.AddSeconds(2));

        Assert.Equal(DocumentOcrJobStatus.Completed, job.Status);
        Assert.Equal(0.98m, job.Confidence);
        Assert.False(job.NeedsReview);
        Assert.Equal(OcrAttemptOutcome.Succeeded, attempt.Outcome);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void CompleteRejectsConfidenceOutsideValidRange(double confidence)
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => job.Complete(
            attempt.Id,
            OcrDocumentType.CommercialInvoice,
            "{}",
            null,
            (decimal)confidence,
            true,
            null,
            Now.AddSeconds(1)));
    }

    [Fact]
    public void CompleteRejectsMalformedNormalizedJson()
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        Assert.Throws<ArgumentException>(() => job.Complete(
            attempt.Id,
            OcrDocumentType.CommercialInvoice,
            "not-json",
            null,
            0.5m,
            true,
            null,
            Now.AddSeconds(1)));
        Assert.Equal(DocumentOcrJobStatus.Processing, job.Status);
    }

    [Fact]
    public void TransientFailureCanBeScheduledForRetry()
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        job.RecordFailure(
            attempt.Id,
            OcrAttemptOutcome.TransientFailure,
            "provider_unavailable",
            "Provider unavailable.",
            Now.AddSeconds(1));
        job.ScheduleRetry(Now.AddMinutes(1), Now.AddSeconds(2));

        Assert.Equal(DocumentOcrJobStatus.Queued, job.Status);
        Assert.Equal(Now.AddMinutes(1), job.NextAttemptAt);
        Assert.Equal(OcrAttemptOutcome.TransientFailure, attempt.Outcome);
    }

    [Fact]
    public void PermanentFailureCannotBeScheduledForRetry()
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        job.RecordFailure(
            attempt.Id,
            OcrAttemptOutcome.PermanentFailure,
            "invalid_document",
            "Document cannot be processed.",
            Now.AddSeconds(1));

        Assert.Equal(DocumentOcrJobStatus.Failed, job.Status);
        Assert.Throws<InvalidOperationException>(() =>
            job.ScheduleRetry(Now.AddMinutes(1), Now.AddSeconds(2)));
    }

    [Fact]
    public void FailureDiagnosticsAreBoundedBeforeStateChanges()
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => job.RecordFailure(
            attempt.Id,
            OcrAttemptOutcome.PermanentFailure,
            "provider_error",
            new string('x', 2_001),
            Now.AddSeconds(1)));
        Assert.Equal(DocumentOcrJobStatus.Processing, job.Status);
        Assert.Equal(OcrAttemptOutcome.Processing, attempt.Outcome);
    }

    [Fact]
    public void TerminalJobRejectsFurtherTransitions()
    {
        var job = CreateJob();
        var attempt = job.Start("deterministic", Now, Now.AddMinutes(2));
        job.Complete(
            attempt.Id,
            OcrDocumentType.Other,
            "{}",
            null,
            0.5m,
            true,
            null,
            Now.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() =>
            job.Start("deterministic", Now.AddMinutes(1), Now.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => job.Cancel(Now.AddMinutes(1)));
    }

    [Fact]
    public void CancelledJobIsTerminal()
    {
        var job = CreateJob();

        job.Cancel(Now.AddSeconds(1));

        Assert.Equal(DocumentOcrJobStatus.Cancelled, job.Status);
        Assert.Equal(Now.AddSeconds(1), job.CancelledAt);
        Assert.Throws<InvalidOperationException>(() =>
            job.Start("deterministic", Now.AddMinutes(1), Now.AddMinutes(3)));
    }

    private static DocumentOcrJob CreateJob(
        Guid? tenantId = null,
        string storageReference = "objects/tenant/document.pdf",
        long sizeBytes = 1_024,
        Guid? externalDocumentId = null)
    {
        return DocumentOcrJob.Create(
            tenantId ?? TenantId,
            "request-001",
            storageReference,
            "invoice.pdf",
            "application/pdf",
            sizeBytes,
            OcrDocumentType.CommercialInvoice,
            externalDocumentId ?? DocumentId,
            Guid.CreateVersion7(),
            Now);
    }
}
