using DocumentOcr.Domain.Enums;
using Shared.Entity;

namespace DocumentOcr.Domain.Entities;

public sealed class OcrProviderAttempt : TenantAuditableEntity
{
    private OcrProviderAttempt() { }

    public Guid JobId { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public OcrAttemptOutcome Outcome { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ProviderRequestId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? Diagnostics { get; private set; }

    internal static OcrProviderAttempt Start(
        Guid tenantId,
        Guid jobId,
        string providerName,
        DateTimeOffset startedAt)
    {
        DocumentOcrValidation.RequiredId(tenantId, nameof(tenantId));
        DocumentOcrValidation.RequiredId(jobId, nameof(jobId));
        if (startedAt == default)
            throw new ArgumentException("StartedAt is required.", nameof(startedAt));

        return new OcrProviderAttempt
        {
            TenantId = tenantId,
            JobId = jobId,
            ProviderName = DocumentOcrValidation.RequiredText(providerName, nameof(providerName), 100),
            Outcome = OcrAttemptOutcome.Processing,
            StartedAt = startedAt,
            CreatedAt = startedAt
        };
    }

    internal void Succeed(string? providerRequestId, DateTimeOffset completedAt)
    {
        EnsureProcessing();
        ValidateCompletionTime(completedAt);
        Outcome = OcrAttemptOutcome.Succeeded;
        ProviderRequestId = DocumentOcrValidation.OptionalText(providerRequestId, nameof(providerRequestId), 200);
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }

    internal void Fail(
        OcrAttemptOutcome outcome,
        string errorCode,
        string diagnostics,
        DateTimeOffset completedAt)
    {
        EnsureProcessing();
        ValidateCompletionTime(completedAt);
        if (!Enum.IsDefined(outcome) ||
            outcome is OcrAttemptOutcome.Processing or OcrAttemptOutcome.Succeeded or OcrAttemptOutcome.Cancelled)
            throw new ArgumentOutOfRangeException(nameof(outcome), "A failure outcome is required.");

        Outcome = outcome;
        ErrorCode = DocumentOcrValidation.RequiredText(errorCode, nameof(errorCode), 100);
        Diagnostics = DocumentOcrValidation.RequiredText(diagnostics, nameof(diagnostics), 2_000);
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }

    internal void Cancel(DateTimeOffset cancelledAt)
    {
        EnsureProcessing();
        ValidateCompletionTime(cancelledAt);
        Outcome = OcrAttemptOutcome.Cancelled;
        CompletedAt = cancelledAt;
        UpdatedAt = cancelledAt;
    }

    private void EnsureProcessing()
    {
        if (Outcome != OcrAttemptOutcome.Processing)
            throw new InvalidOperationException("Provider attempt is already complete.");
    }

    private void ValidateCompletionTime(DateTimeOffset completedAt)
    {
        if (completedAt == default)
            throw new ArgumentException("Completion time is required.", nameof(completedAt));
        if (completedAt < StartedAt)
            throw new ArgumentOutOfRangeException(nameof(completedAt), "Completion time cannot precede start time.");
    }
}
