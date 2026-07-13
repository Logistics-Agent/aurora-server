using ShipmentWorkflow.Domain.Entities;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record CargoItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public double WeightKg { get; init; }
    public string? HsCode { get; init; }

    public static CargoItemDto FromEntity(CargoItem cargoItem)
    {
        return new CargoItemDto
        {
            Id = cargoItem.Id,
            Name = cargoItem.Name,
            Quantity = cargoItem.Quantity,
            WeightKg = cargoItem.WeightKg,
            HsCode = cargoItem.HsCode
        };
    }
}
