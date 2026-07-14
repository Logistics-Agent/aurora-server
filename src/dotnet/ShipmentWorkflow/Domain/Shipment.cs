using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class Shipment : TenantAuditableEntity
{
    private static readonly IReadOnlyDictionary<ShipmentStatus, ShipmentStatus[]> AllowedTransitions =
        new Dictionary<ShipmentStatus, ShipmentStatus[]>
        {
            [ShipmentStatus.Draft] = [ShipmentStatus.Submitted, ShipmentStatus.Cancelled],
            [ShipmentStatus.Submitted] = [ShipmentStatus.Planning, ShipmentStatus.Cancelled],
            [ShipmentStatus.Planning] = [ShipmentStatus.Negotiating, ShipmentStatus.Cancelled],
            [ShipmentStatus.Negotiating] = [ShipmentStatus.Confirmed, ShipmentStatus.Cancelled],
            [ShipmentStatus.Confirmed] = [ShipmentStatus.PickedUp, ShipmentStatus.Cancelled],
            [ShipmentStatus.PickedUp] = [ShipmentStatus.InTransit, ShipmentStatus.Cancelled],
            [ShipmentStatus.InTransit] = [ShipmentStatus.CustomsProcessing, ShipmentStatus.Delivered, ShipmentStatus.Cancelled],
            [ShipmentStatus.CustomsProcessing] = [ShipmentStatus.InTransit, ShipmentStatus.Delivered, ShipmentStatus.Cancelled],
            [ShipmentStatus.Delivered] = [ShipmentStatus.Completed],
            [ShipmentStatus.Completed] = [],
            [ShipmentStatus.Cancelled] = []
        };

    private Shipment() { }

    public static Shipment Create(
        Guid tenantId,
        string shipmentNo,
        string? orderId,
        string customerName,
        string destinationAddress)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(shipmentNo))
        {
            throw new ArgumentException("Shipment number is required.", nameof(shipmentNo));
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("Customer name is required.", nameof(customerName));
        }

        if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            throw new ArgumentException("Destination address is required.", nameof(destinationAddress));
        }

        return new Shipment
        {
            TenantId = tenantId,
            ShipmentNo = shipmentNo.Trim(),
            OrderId = string.IsNullOrWhiteSpace(orderId) ? null : orderId.Trim(),
            CustomerName = customerName.Trim(),
            DestinationAddress = destinationAddress.Trim(),
            Status = ShipmentStatus.Draft,
            Priority = ShipmentPriority.Normal,
            TransportMode = TransportMode.Unknown
        };
    }

    public string ShipmentNo { get; set; } = string.Empty;
    public string? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public ShipmentPriority Priority { get; set; } = ShipmentPriority.Normal;
    public TransportMode TransportMode { get; set; } = TransportMode.Unknown;
    public string? RouteId { get; set; }
    public string? VehicleId { get; set; }
    public DateTimeOffset? EstimatedPickupTime { get; set; }
    public DateTimeOffset? EstimatedDeliveryTime { get; set; }
    public DateTimeOffset? ActualPickupTime { get; set; }
    public DateTimeOffset? ActualDeliveryTime { get; set; }
    public string? Notes { get; set; }

    public ICollection<CargoItem> CargoItems { get; set; } = [];
    public ICollection<ShipmentLocation> Locations { get; set; } = [];
    public ICollection<ShipmentDocument> Documents { get; set; } = [];
    public ICollection<ShipmentMilestone> Milestones { get; set; } = [];
    public ICollection<ShipmentStatusHistory> StatusHistories { get; set; } = [];

    public bool IsTerminal => Status is ShipmentStatus.Completed or ShipmentStatus.Cancelled;

    public static bool CanTransition(ShipmentStatus from, ShipmentStatus to)
    {
        return AllowedTransitions.TryGetValue(NormalizeStatus(from), out var allowed) &&
            allowed.Contains(NormalizeStatus(to));
    }

    public void Submit(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.Submitted, "Shipment submitted.", MilestoneSource.User, actorId);
    }

    public void StartPlanning(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.Planning, "Shipment planning started.", MilestoneSource.Staff, actorId);
    }

    public void StartNegotiation(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.Negotiating, "Shipment negotiation started.", MilestoneSource.Staff, actorId);
    }

    public void Confirm(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.Confirmed, "Shipment confirmed.", MilestoneSource.Staff, actorId);
    }

    public void MarkPickedUp(Guid? actorId = null, DateTimeOffset? pickedUpAt = null)
    {
        ActualPickupTime = pickedUpAt ?? DateTimeOffset.UtcNow;
        TransitionTo(ShipmentStatus.PickedUp, "Shipment picked up.", MilestoneSource.Staff, actorId, ActualPickupTime);
    }

    public void MarkInTransit(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.InTransit, "Shipment in transit.", MilestoneSource.Staff, actorId);
    }

    public void StartCustomsProcessing(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.CustomsProcessing, "Customs processing started.", MilestoneSource.Staff, actorId);
    }

    public void MarkDelivered(Guid? actorId = null, DateTimeOffset? deliveredAt = null)
    {
        ActualDeliveryTime = deliveredAt ?? DateTimeOffset.UtcNow;
        TransitionTo(ShipmentStatus.Delivered, "Shipment delivered.", MilestoneSource.Staff, actorId, ActualDeliveryTime);
    }

    public void Complete(Guid? actorId = null)
    {
        TransitionTo(ShipmentStatus.Completed, "Shipment completed.", MilestoneSource.Staff, actorId);
    }

    public void Cancel(string reason, Guid? actorId = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));
        }

        TransitionTo(ShipmentStatus.Cancelled, $"Shipment cancelled: {reason.Trim()}", MilestoneSource.User, actorId);
    }

    public void AssignRoute(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
        {
            throw new ArgumentException("RouteId is required.", nameof(routeId));
        }

        RouteId = routeId.Trim();
    }

    public void AssignVehicle(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            throw new ArgumentException("VehicleId is required.", nameof(vehicleId));
        }

        VehicleId = vehicleId.Trim();
    }

    public void AddCargoItem(
        string name,
        int quantity,
        double weightKg,
        string? hsCode = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Cargo item name is required.", nameof(name));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Cargo quantity must be greater than zero.", nameof(quantity));
        }

        if (weightKg < 0)
        {
            throw new ArgumentException("Cargo weight must not be negative.", nameof(weightKg));
        }

        CargoItems.Add(new CargoItem
        {
            ShipmentId = Id,
            Name = name.Trim(),
            Quantity = quantity,
            WeightKg = weightKg,
            HsCode = string.IsNullOrWhiteSpace(hsCode) ? null : hsCode.Trim()
        });
    }

    public void ChangeStatus(ShipmentStatus newStatus, string? note = null)
    {
        TransitionTo(newStatus, note ?? $"Shipment status changed to {NormalizeStatus(newStatus)}.", MilestoneSource.System);
    }

    public void AddLocation(
        LocationType type,
        string name,
        string address,
        int sequence,
        double? latitude = null,
        double? longitude = null,
        string? contactName = null,
        string? contactPhone = null)
    {
        Locations.Add(ShipmentLocation.Create(
            TenantId,
            Id,
            type,
            name,
            address,
            sequence,
            latitude,
            longitude,
            contactName,
            contactPhone));
    }

    public void AddDocumentMetadata(
        string fileName,
        DocumentType documentType,
        string storageUrl,
        Guid? uploadedBy,
        DateTimeOffset uploadedAt,
        OCRStatus ocrStatus = OCRStatus.Pending,
        decimal? ocrConfidence = null,
        string? extractedDataJson = null)
    {
        Documents.Add(ShipmentDocument.Create(
            TenantId,
            Id,
            fileName,
            documentType,
            storageUrl,
            uploadedBy,
            uploadedAt,
            ocrStatus,
            ocrConfidence,
            extractedDataJson));
    }

    public void AddMilestone(
        ShipmentStatus status,
        string? description,
        DateTimeOffset recordedAt,
        MilestoneSource source,
        Guid? createdBy,
        double? latitude = null,
        double? longitude = null)
    {
        Milestones.Add(ShipmentMilestone.Create(
            TenantId,
            Id,
            NormalizeStatus(status),
            description,
            recordedAt,
            source,
            createdBy,
            latitude,
            longitude));
    }

    private void TransitionTo(
        ShipmentStatus newStatus,
        string note,
        MilestoneSource milestoneSource,
        Guid? actorId = null,
        DateTimeOffset? recordedAt = null)
    {
        var currentStatus = NormalizeStatus(Status);
        var targetStatus = NormalizeStatus(newStatus);

        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition shipment from {currentStatus} to {targetStatus}.");
        }

        var timestamp = recordedAt ?? DateTimeOffset.UtcNow;
        Status = targetStatus;

        StatusHistories.Add(new ShipmentStatusHistory
        {
            ShipmentId = Id,
            Status = targetStatus,
            Note = note,
            CreatedAt = timestamp
        });

        AddMilestone(
            targetStatus,
            note,
            timestamp,
            milestoneSource,
            actorId);
    }

    private static ShipmentStatus NormalizeStatus(ShipmentStatus status)
    {
        return status switch
        {
            ShipmentStatus.Created => ShipmentStatus.Draft,
            ShipmentStatus.CustomsChecking => ShipmentStatus.CustomsProcessing,
            _ => status
        };
    }
}
