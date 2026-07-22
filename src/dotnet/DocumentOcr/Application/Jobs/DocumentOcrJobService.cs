using System.Text.Json;
using DocumentOcr.Application.Providers;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Exceptions;
using Shared.Security;

namespace DocumentOcr.Application.Jobs;

public sealed record SubmitDocumentJobInput(
    string IdempotencyKey,
    string StorageReference,
    string FileName,
    string MimeType,
    long SizeBytes,
    OcrDocumentType DocumentTypeHint,
    Guid ExternalDocumentId,
    Guid? ExternalShipmentId);

public sealed record ListDocumentJobsInput(
    int Page,
    int PageSize,
    DocumentOcrJobStatus? Status,
    Guid? ExternalDocumentId,
    Guid? ExternalShipmentId);

public sealed record DocumentOcrJobPage(
    IReadOnlyList<DocumentOcrJob> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public interface IDocumentOcrJobService
{
    Task<DocumentOcrJob> SubmitAsync(
        SubmitDocumentJobInput input,
        CancellationToken cancellationToken = default);
    Task<DocumentOcrJob> GetAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<DocumentOcrJobPage> ListAsync(
        ListDocumentJobsInput input,
        CancellationToken cancellationToken = default);
    Task<DocumentOcrJob?> ProcessAsync(
        Guid tenantId,
        Guid jobId,
        CancellationToken cancellationToken = default);
}

public interface IDocumentOcrJobProcessor
{
    Task<DocumentOcrJob?> ProcessClaimedAsync(
        Guid tenantId,
        Guid jobId,
        Guid attemptId,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentOcrJobService(
    DocumentOcrDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    DocumentProcessingOptions options,
    DocumentOcrWorkerOptions workerOptions,
    DocumentInputPolicy inputPolicy,
    IDocumentContentReader contentReader,
    IOcrProvider provider) : IDocumentOcrJobService, IDocumentOcrJobProcessor
{
    public async Task<DocumentOcrJob> SubmitAsync(
        SubmitDocumentJobInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = RequireTenant();
        inputPolicy.ValidateMetadata(
            input.StorageReference, input.FileName, input.MimeType, input.SizeBytes);
        var idempotencyKey = Required(input.IdempotencyKey, nameof(input.IdempotencyKey), 150);

        var existing = await dbContext.Jobs.SingleOrDefaultAsync(
            job => job.TenantId == tenantId && job.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
            return existing;

        var job = DocumentOcrJob.Create(
            tenantId,
            idempotencyKey,
            input.StorageReference,
            input.FileName,
            input.MimeType,
            input.SizeBytes,
            input.DocumentTypeHint,
            input.ExternalDocumentId,
            input.ExternalShipmentId,
            timeProvider.GetUtcNow());
        dbContext.Jobs.Add(job);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return job;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            return await dbContext.Jobs.SingleAsync(
                item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        }
    }

    public async Task<DocumentOcrJob> GetAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId is required.", nameof(jobId));

        return await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                job => job.TenantId == tenantId && job.Id == jobId,
                cancellationToken)
            ?? throw new NotFoundException("Document OCR job was not found.");
    }

    public async Task<DocumentOcrJobPage> ListAsync(
        ListDocumentJobsInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = RequireTenant();
        var page = input.Page == 0 ? 1 : input.Page;
        var pageSize = input.PageSize == 0 ? 20 : input.PageSize;
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(input.Page), "Page must be positive.");
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input.PageSize), "PageSize must be between 1 and 100.");
        if (page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(input.Page), "Page is too large.");

        var query = dbContext.Jobs.AsNoTracking().Where(job => job.TenantId == tenantId);
        if (input.Status.HasValue)
            query = query.Where(job => job.Status == input.Status.Value);
        if (input.ExternalDocumentId.HasValue)
            query = query.Where(job => job.ExternalDocumentId == input.ExternalDocumentId.Value);
        if (input.ExternalShipmentId.HasValue)
            query = query.Where(job => job.ExternalShipmentId == input.ExternalShipmentId.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(job => job.CreatedAt)
            .ThenByDescending(job => job.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        return new DocumentOcrJobPage(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<DocumentOcrJob?> ProcessAsync(
        Guid tenantId,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        RequiredId(tenantId, nameof(tenantId));
        RequiredId(jobId, nameof(jobId));
        var job = await dbContext.Jobs
            .IgnoreQueryFilters()
            .Include(item => item.Attempts)
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == jobId,
                cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (job is null || job.Status != DocumentOcrJobStatus.Queued || job.NextAttemptAt > now)
            return null;

        var attempt = job.Start(provider.Name, now, now.Add(workerOptions.LeaseDuration));
        dbContext.ProviderAttempts.Add(attempt);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }

        return await ProcessClaimedJobAsync(job, attempt, cancellationToken);
    }

    public async Task<DocumentOcrJob?> ProcessClaimedAsync(
        Guid tenantId,
        Guid jobId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        RequiredId(tenantId, nameof(tenantId));
        RequiredId(jobId, nameof(jobId));
        RequiredId(attemptId, nameof(attemptId));
        var job = await dbContext.Jobs
            .IgnoreQueryFilters()
            .Include(item => item.Attempts)
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == jobId,
                cancellationToken);
        if (job is null || job.Status != DocumentOcrJobStatus.Processing)
            return null;
        var attempt = job.Attempts.SingleOrDefault(item =>
            item.Id == attemptId && item.Outcome == OcrAttemptOutcome.Processing);
        return attempt is null
            ? null
            : await ProcessClaimedJobAsync(job, attempt, cancellationToken);
    }

    private async Task<DocumentOcrJob> ProcessClaimedJobAsync(
        DocumentOcrJob job,
        OcrProviderAttempt attempt,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await contentReader.ReadAsync(
                new DocumentContentRequest(
                    job.TenantId,
                    job.StorageReference,
                    job.FileName,
                    job.MimeType,
                    job.SizeBytes),
                cancellationToken);
            inputPolicy.ValidateContent(content);
            var result = await provider.ExtractAsync(
                new OcrProviderRequest(
                    job.Id,
                    job.TenantId,
                    job.FileName,
                    job.MimeType,
                    job.DocumentTypeHint,
                    content),
                cancellationToken);
            var normalized = DocumentOcrResultNormalizer.Normalize(
                result, options.ReviewConfidenceThreshold);
            var completedAt = timeProvider.GetUtcNow();
            job.Complete(
                attempt.Id,
                result.DetectedDocumentType,
                normalized.Json,
                normalized.FieldConfidenceJson,
                normalized.Confidence,
                normalized.NeedsReview,
                result.ProviderRequestId,
                completedAt);
            AddCompletedOutbox(job, completedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OcrProviderException exception) when (exception.Kind == OcrProviderFailureKind.Cancelled)
        {
            job.Cancel(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            job.Cancel(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OcrProviderException exception)
        {
            await RecordFailureAsync(
                job,
                attempt.Id,
                MapFailure(exception.Kind),
                exception.Code,
                exception.Message,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await RecordFailureAsync(
                job,
                attempt.Id,
                OcrAttemptOutcome.InvalidDocument,
                "invalid_document",
                BoundedError(exception.Message),
                cancellationToken);
        }

        return job;
    }

    private async Task RecordFailureAsync(
        DocumentOcrJob job,
        Guid attemptId,
        OcrAttemptOutcome outcome,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var failedAt = timeProvider.GetUtcNow();
        job.RecordFailure(attemptId, outcome, errorCode, errorMessage, failedAt);
        if (outcome == OcrAttemptOutcome.TransientFailure && job.AttemptCount < workerOptions.MaxAttempts)
        {
            var delay = DocumentOcrRetryPolicy.GetDelay(job.Id, job.AttemptCount, workerOptions);
            job.ScheduleRetry(failedAt.Add(delay), failedAt);
        }
        else
        {
            dbContext.OutboxMessages.Add(DocumentOcrOutboxFactory.CreateFailed(job, failedAt));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddCompletedOutbox(DocumentOcrJob job, DateTimeOffset occurredAt)
    {
        var integrationEvent = new DocumentOcrCompletedEvent
        {
            TenantId = job.TenantId,
            JobId = job.Id,
            ExternalDocumentId = job.ExternalDocumentId,
            ExternalShipmentId = job.ExternalShipmentId,
            DetectedDocumentType = job.DetectedDocumentType!.Value.ToString(),
            NormalizedJson = job.NormalizedJson!,
            Confidence = job.Confidence!.Value,
            NeedsReview = job.NeedsReview!.Value,
            OccurredAt = occurredAt
        };
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            job.TenantId,
            integrationEvent.EventId,
            nameof(DocumentOcrCompletedEvent),
            JsonSerializer.Serialize(integrationEvent),
            occurredAt));
    }

    private Guid RequireTenant() => currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
        ? tenantId
        : throw new DomainException("TenantId was not found in the authenticated user context.");

    private static OcrAttemptOutcome MapFailure(OcrProviderFailureKind kind) => kind switch
    {
        OcrProviderFailureKind.Transient => OcrAttemptOutcome.TransientFailure,
        OcrProviderFailureKind.Permanent => OcrAttemptOutcome.PermanentFailure,
        OcrProviderFailureKind.InvalidDocument => OcrAttemptOutcome.InvalidDocument,
        OcrProviderFailureKind.UnsupportedFormat => OcrAttemptOutcome.UnsupportedFormat,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static Guid RequiredId(Guid value, string parameterName) => value != Guid.Empty
        ? value
        : throw new ArgumentException($"{parameterName} is required.", parameterName);

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string BoundedError(string? value)
    {
        const string fallback = "Document processing failed.";
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= 2_000 ? normalized : normalized[..2_000];
    }
}
