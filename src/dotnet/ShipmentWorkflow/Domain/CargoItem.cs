using Shared.Entity;

namespace ShipmentWorkflow.Domain.Entities;

public class CargoItem : AuditableEntity
{
    public Guid ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double WeightKg { get; set; }
    public string? HsCode { get; set; }
}
