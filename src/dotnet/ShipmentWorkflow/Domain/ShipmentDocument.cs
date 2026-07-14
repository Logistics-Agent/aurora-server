using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class ShipmentDocument : TenantAuditableEntity
{
    public const int FileNameMaxLength = 255;
    public const int StorageUrlMaxLength = 1_000;

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
        string? extractedDataJson = null)
    {
        ValidateTenantAndShipment(tenantId, shipmentId);
        ValidateRequiredText(fileName, nameof(fileName), FileNameMaxLength);
        ValidateRequiredText(storageUrl, nameof(storageUrl), StorageUrlMaxLength);
        ValidateOcrConfidence(ocrConfidence);

        if (uploadedAt == default)
        {
            throw new ArgumentException("UploadedAt is required.", nameof(uploadedAt));
        }

        return new ShipmentDocument
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            FileName = fileName.Trim(),
            DocumentType = documentType,
            StorageUrl = storageUrl.Trim(),
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
    public OCRStatus OCRStatus { get; private set; }
    public decimal? OCRConfidence { get; private set; }
    public Guid? UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public string? ExtractedDataJson { get; private set; }

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
