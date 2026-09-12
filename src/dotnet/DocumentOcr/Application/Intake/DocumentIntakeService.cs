using DocumentOcr.Application.Uploads;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shared.Exceptions;
using Shared.Security;

namespace DocumentOcr.Application.Intake;

public sealed record CreateDocumentIntakeInput(
    Guid UploadId,
    string IdempotencyKey,
    OcrDocumentType DocumentTypeHint,
    DocumentOcrPurpose Purpose,
    string? ExternalReference = null,
    Guid? InitiatingCorrelationId = null);

public interface IDocumentIntakeService
{
    Task<DocumentOcrJob> CreateAsync(
        CreateDocumentIntakeInput input,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentIntakeService(
    DocumentOcrDbContext dbContext,
    DocumentUploadService uploadService,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IDocumentIntakeService
{
    public async Task<DocumentOcrJob> CreateAsync(
        CreateDocumentIntakeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = RequireTenant();
        var uploadId = DocumentOcrValidation.RequiredId(input.UploadId, nameof(input.UploadId));
        var idempotencyKey = DocumentOcrValidation.RequiredText(input.IdempotencyKey, nameof(input.IdempotencyKey), 150);
        var externalReference = DocumentOcrValidation.OptionalText(input.ExternalReference, nameof(input.ExternalReference), 150);
        ValidateInput(input.DocumentTypeHint, input.Purpose);

        await uploadService.VerifyAsync(uploadId, cancellationToken);
        var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var session = await dbContext.UploadSessions.SingleAsync(
                item => item.TenantId == tenantId && item.Id == uploadId,
                cancellationToken);
            var existing = await dbContext.Jobs.SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
            if (existing is not null)
                return await ReplayOrConflictAsync(existing, input, idempotencyKey, externalReference, transaction, cancellationToken);

            var linkedJob = await dbContext.Jobs.SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.UploadId == uploadId,
                cancellationToken);
            if (linkedJob is not null)
                return await ReplayOrConflictAsync(linkedJob, input, idempotencyKey, externalReference, transaction, cancellationToken);

            if (session.Status != DocumentUploadStatus.Uploaded)
                throw new DocumentUploadValidationException(
                    "UPLOAD_NOT_VERIFIED", "The upload session must be verified before it is consumed.");

            var now = timeProvider.GetUtcNow();
            var job = DocumentOcrJob.Create(
                tenantId,
                idempotencyKey,
                session.ObjectKey,
                session.FileName,
                session.VerifiedMimeType ?? session.DeclaredMimeType,
                session.VerifiedSizeBytes ?? session.DeclaredSizeBytes,
                input.DocumentTypeHint,
                uploadId,
                null,
                now,
                OcrExtractionMode.Structured,
                externalReference,
                input.Purpose,
                input.InitiatingCorrelationId ?? DocumentOcrCorrelationId.FromTrace(currentUser.TraceId),
                uploadId);
            dbContext.Jobs.Add(job);
            session.MarkConsumed(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return job;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw new DocumentUploadValidationException(
                "UPLOAD_VERIFICATION_IN_PROGRESS",
                "The upload session changed while the intake was being created. Retry the same request.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var candidates = await dbContext.Jobs.AsNoTracking().Where(
                item => item.TenantId == tenantId &&
                    (item.IdempotencyKey == idempotencyKey || item.UploadId == uploadId))
                .ToListAsync(cancellationToken);
            var existing = candidates.SingleOrDefault(item => item.MatchesIntakeRequest(
                    uploadId,
                    idempotencyKey,
                    input.DocumentTypeHint,
                    input.Purpose,
                    externalReference));
            if (candidates.Count != 1 || existing is null)
            {
                throw new ConflictException(
                    "The idempotency key or upload is already used for a different document intake request.");
            }
            return existing;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<DocumentOcrJob> ReplayOrConflictAsync(
        DocumentOcrJob existing,
        CreateDocumentIntakeInput input,
        string idempotencyKey,
        string? externalReference,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (!existing.MatchesIntakeRequest(
                input.UploadId,
                idempotencyKey,
                input.DocumentTypeHint,
                input.Purpose,
                externalReference))
        {
            throw new ConflictException("The idempotency key is already used for a different document intake request.");
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return existing;
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private Guid RequireTenant() =>
        currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new DomainException("Tenant context is required.");

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static void ValidateInput(OcrDocumentType documentTypeHint, DocumentOcrPurpose purpose)
    {
        if (!Enum.IsDefined(documentTypeHint) || documentTypeHint == OcrDocumentType.Unspecified)
            throw new ArgumentException("DocumentTypeHint is invalid.", nameof(documentTypeHint));
        if (!Enum.IsDefined(purpose) || purpose == DocumentOcrPurpose.Unspecified)
            throw new ArgumentException("Purpose is invalid.", nameof(purpose));
    }
}
