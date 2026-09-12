using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class ShipmentDocument : TenantAuditableEntity
{
    public const int FileNameMaxLength = 255;
    public const int StorageUrlMaxLength = 1_000;
    public const int IdempotencyKeyMaxLength = 150;
    public const int RequestHashMaxLength = 64;

    private ShipmentDocument() { }

    internal static ShipmentDocument Create(
        Guid tenantId,
        Guid shipmentId,
        string fileName,
        DocumentType documentType,
        string storageUrl,
        Guid? uploadedBy,
        DateTimeOffset uploadedAt,
        OCRStatus ocrStatus = OCRStatus.Pending,
        decimal? ocrConfidence = null,
        string? extractedDataJson = null,
        string? idempotencyKey = null,
        Guid? uploadId = null,
        string? storageReference = null,
        string? requestHash = null,
        Guid? documentId = null)
    {
        ValidateTenantAndShipment(tenantId, shipmentId);
        ValidateRequiredText(fileName, nameof(fileName), FileNameMaxLength);
        var normalizedStorageUrl = RequireStorageReference(storageUrl, storageReference);
        ValidateOcrConfidence(ocrConfidence);
        ValidateIdempotency(idempotencyKey, uploadId, requestHash);

        if (uploadedAt == default)
        {
            throw new ArgumentException("UploadedAt is required.", nameof(uploadedAt));
        }

        return new ShipmentDocument
        {
            Id = documentId ?? Guid.CreateVersion7(),
            TenantId = tenantId,
            ShipmentId = shipmentId,
            FileName = fileName.Trim(),
            DocumentType = documentType,
            StorageUrl = normalizedStorageUrl,
            StorageReference = string.IsNullOrWhiteSpace(storageReference)
                ? normalizedStorageUrl
                : storageReference.Trim(),
            IdempotencyKey = idempotencyKey?.Trim(),
            UploadId = uploadId,
            RequestHash = requestHash?.Trim().ToLowerInvariant(),
            OCRStatus = ocrStatus,
            OCRConfidence = ocrConfidence,
            UploadedBy = uploadedBy,
            UploadedAt = uploadedAt,
            ExtractedDataJson = string.IsNullOrWhiteSpace(extractedDataJson)
                ? null
                : extractedDataJson.Trim()
        };
    }

    public Guid ShipmentId { get; private set; }
    public Shipment? Shipment { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public DocumentType DocumentType { get; private set; }
    public string StorageUrl { get; private set; } = string.Empty;
    public string? StorageReference { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public Guid? UploadId { get; private set; }
    public string? RequestHash { get; private set; }
    public OCRStatus OCRStatus { get; private set; }
    public decimal? OCRConfidence { get; private set; }
    public Guid? UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public string? ExtractedDataJson { get; private set; }

    internal void UpdateOcrMetadata(
        OCRStatus ocrStatus,
        decimal? ocrConfidence = null,
        string? extractedDataJson = null)
    {
        ValidateOcrConfidence(ocrConfidence);

        OCRStatus = ocrStatus;
        OCRConfidence = ocrConfidence;
        ExtractedDataJson = string.IsNullOrWhiteSpace(extractedDataJson)
            ? null
            : extractedDataJson.Trim();
    }

    private static void ValidateTenantAndShipment(Guid tenantId, Guid shipmentId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("ShipmentId is required.", nameof(shipmentId));
        }
    }

    private static void ValidateOcrConfidence(decimal? ocrConfidence)
    {
        if (ocrConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ocrConfidence),
                "OCR confidence must be between 0 and 1.");
        }
    }

    private static string RequireStorageReference(string storageUrl, string? storageReference)
    {
        var normalizedStorageUrl = string.IsNullOrWhiteSpace(storageUrl)
            ? storageReference
            : storageUrl;
        ValidateRequiredText(normalizedStorageUrl!, nameof(storageUrl), StorageUrlMaxLength);
        if (!string.IsNullOrWhiteSpace(storageReference) &&
            storageReference.Trim().Length > StorageUrlMaxLength)
        {
            throw new ArgumentException(
                $"StorageReference must be {StorageUrlMaxLength} characters or fewer.",
                nameof(storageReference));
        }

        return normalizedStorageUrl!.Trim();
    }

    private static void ValidateIdempotency(string? idempotencyKey, Guid? uploadId, string? requestHash)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (uploadId.HasValue || !string.IsNullOrWhiteSpace(requestHash))
                throw new ArgumentException("IdempotencyKey is required for intake metadata.", nameof(idempotencyKey));
            return;
        }

        ValidateRequiredText(idempotencyKey, nameof(idempotencyKey), IdempotencyKeyMaxLength);
        if (!uploadId.HasValue || uploadId.Value == Guid.Empty)
            throw new ArgumentException("UploadId is required for idempotent attachment.", nameof(uploadId));
        ValidateRequiredText(requestHash!, nameof(requestHash), RequestHashMaxLength);
    }

    private static void ValidateRequiredText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{name} must be {maxLength} characters or fewer.", name);
        }
    }
}
