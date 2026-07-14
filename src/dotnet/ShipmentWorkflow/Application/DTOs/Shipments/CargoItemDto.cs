using ShipmentWorkflow.Domain.Entities;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record CargoItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public double WeightKg { get; init; }
    public string? HsCode { get; init; }
    public string? Description { get; init; }
    public string? Unit { get; init; }
    public double? VolumeM3 { get; init; }
    public decimal? DeclaredValue { get; init; }
    public string? Currency { get; init; }
    public bool IsDangerousGoods { get; init; }
    public string? PackageType { get; init; }

    public static CargoItemDto FromEntity(CargoItem cargoItem)
    {
        return new CargoItemDto
        {
            Id = cargoItem.Id,
            Name = cargoItem.Name,
            Quantity = cargoItem.Quantity,
            WeightKg = cargoItem.WeightKg,
            HsCode = cargoItem.HsCode,
            Description = cargoItem.Description,
            Unit = cargoItem.Unit,
            VolumeM3 = cargoItem.VolumeM3,
            DeclaredValue = cargoItem.DeclaredValue,
            Currency = cargoItem.Currency,
            IsDangerousGoods = cargoItem.IsDangerousGoods,
            PackageType = cargoItem.PackageType
        };
    }
}
