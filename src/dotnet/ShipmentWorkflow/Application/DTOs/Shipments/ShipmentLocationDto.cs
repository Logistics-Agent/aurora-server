using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ShipmentLocationDto
{
    public Guid Id { get; init; }
    public LocationType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public int Sequence { get; init; }

    public static ShipmentLocationDto FromEntity(ShipmentLocation location)
    {
        return new ShipmentLocationDto
        {
            Id = location.Id,
            Type = location.Type,
            Name = location.Name,
            Address = location.Address,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            ContactName = location.ContactName,
            ContactPhone = location.ContactPhone,
            Sequence = location.Sequence
        };
    }
}
