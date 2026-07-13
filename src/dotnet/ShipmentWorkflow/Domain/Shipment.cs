using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class Shipment : TenantAuditableEntity
{
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
            Status = ShipmentStatus.Created
        };
    }

    public string ShipmentNo { get; set; } = string.Empty;
    public string? OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }

    public ICollection<CargoItem> CargoItems { get; set; } = [];
    public ICollection<ShipmentStatusHistory> StatusHistories { get; set; } = [];

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
        var oldStatus = Status;
        Status = newStatus;

        StatusHistories.Add(new ShipmentStatusHistory
        {
            ShipmentId = Id,
            Status = newStatus,
            Note = note,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
