using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ShipmentDocumentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public DocumentType DocumentType { get; init; }
    public string StorageUrl { get; init; } = string.Empty;
    public OCRStatus OCRStatus { get; init; }
    public decimal? OCRConfidence { get; init; }
    public Guid? UploadedBy { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
    public string? ExtractedDataJson { get; init; }

    public static ShipmentDocumentDto FromEntity(ShipmentDocument document)
    {
        return new ShipmentDocumentDto
        {
            Id = document.Id,
            FileName = document.FileName,
            DocumentType = document.DocumentType,
            StorageUrl = document.StorageUrl,
            OCRStatus = document.OCRStatus,
            OCRConfidence = document.OCRConfidence,
            UploadedBy = document.UploadedBy,
            UploadedAt = document.UploadedAt,
            ExtractedDataJson = document.ExtractedDataJson
        };
    }
}
