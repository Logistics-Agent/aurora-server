using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class ShipmentMilestone : TenantAuditableEntity
{
    public const int DescriptionMaxLength = 500;

    private ShipmentMilestone() { }

    internal static ShipmentMilestone Create(
        Guid tenantId,
        Guid shipmentId,
        ShipmentStatus status,
        string? description,
        DateTimeOffset recordedAt,
        MilestoneSource source,
        Guid? createdBy,
        double? latitude = null,
        double? longitude = null)
    {
        ValidateTenantAndShipment(tenantId, shipmentId);
        ValidateCoordinates(latitude, longitude);

        if (recordedAt == default)
        {
            throw new ArgumentException("RecordedAt is required.", nameof(recordedAt));
        }

        return new ShipmentMilestone
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            Status = status,
            Description = NormalizeOptionalText(description, DescriptionMaxLength, nameof(description)),
            Latitude = latitude,
            Longitude = longitude,
            RecordedAt = recordedAt,
            Source = source,
            CreatedByUserId = createdBy
        };
    }

    public Guid ShipmentId { get; private set; }
    public Shipment? Shipment { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string? Description { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public MilestoneSource Source { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

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

    private static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        }
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{name} must be {maxLength} characters or fewer.", name);
        }

        return trimmed;
    }
}
