using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class DocumentUploadHttpBridge(
    DocumentOcrDbContext dbContext,
    IDocumentInputStorage inputStorage,
    DocumentUploadBridgeTokenService tokenService,
    TimeProvider timeProvider)
{
    public async Task<IResult> PutAsync(
        Guid tenantId,
        Guid uploadId,
        string? token,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var validation = tokenService.Validate(token, tenantId, uploadId, now, out var payload);
        if (validation == UploadBridgeTokenValidation.Invalid)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (validation == UploadBridgeTokenValidation.Expired)
            return Results.StatusCode(StatusCodes.Status410Gone);

        var session = await dbContext.UploadSessions.IgnoreQueryFilters().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == uploadId,
            cancellationToken);
        if (session is null)
            return Results.NotFound();
        if (session.ExpiresAt <= now)
            return Results.StatusCode(StatusCodes.Status410Gone);
        if (session.Status != DocumentUploadStatus.Pending || payload!.ObjectKey != session.ObjectKey)
            return Results.StatusCode(StatusCodes.Status409Conflict);
        if (request.ContentLength is not long contentLength || contentLength <= 0)
        {
            return Results.BadRequest();
        }
        if (contentLength > session.MaximumSizeBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (contentLength != session.DeclaredSizeBytes)
            return Results.BadRequest();
        if (!string.Equals(request.ContentType, session.DeclaredMimeType, StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        try
        {
            await inputStorage.WriteAsync(
                tenantId,
                session.ObjectKey,
                request.Body,
                session.MaximumSizeBytes,
                cancellationToken);
            return Results.NoContent();
        }
        catch (DocumentInputTooLargeException)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
    }
}
