using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record DocumentIntakeDto
{
    public Guid IntakeId { get; init; }
    public Guid ShipmentId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid UploadId { get; init; }
    public string StorageReference { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DocumentType DocumentType { get; init; }
    public DocumentIntakeStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public static DocumentIntakeDto FromEntity(DocumentIntake intake) => new()
    {
        IntakeId = intake.Id,
        ShipmentId = intake.ShipmentId,
        DocumentId = intake.DocumentId,
        UploadId = intake.UploadId,
        StorageReference = intake.StorageReference,
        FileName = intake.FileName,
        DocumentType = intake.DocumentType,
        Status = intake.Status,
        CreatedAt = intake.CreatedAt,
        UpdatedAt = intake.UpdatedAt
    };
}
