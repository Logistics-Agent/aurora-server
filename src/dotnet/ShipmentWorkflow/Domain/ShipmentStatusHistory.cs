using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class ShipmentStatusHistory : AuditableEntity
{
    public Guid ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public ShipmentStatus Status { get; set; }
    public string? Note { get; set; }
}
