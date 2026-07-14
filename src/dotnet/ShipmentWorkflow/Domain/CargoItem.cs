using Shared.Entity;

namespace ShipmentWorkflow.Domain.Entities;

public class CargoItem : AuditableEntity
{
    public Guid ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? HsCode { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public double WeightKg { get; set; }
    public double? VolumeM3 { get; set; }
    public decimal? DeclaredValue { get; set; }
    public string? Currency { get; set; }
    public bool IsDangerousGoods { get; set; }
    public string? PackageType { get; set; }
}
