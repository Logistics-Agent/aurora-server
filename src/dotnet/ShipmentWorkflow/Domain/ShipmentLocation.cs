using Shared.Entity;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public class ShipmentLocation : TenantAuditableEntity
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 500;
    public const int ContactNameMaxLength = 200;
    public const int ContactPhoneMaxLength = 50;

    private ShipmentLocation() { }

    internal static ShipmentLocation Create(
        Guid tenantId,
        Guid shipmentId,
        LocationType type,
        string name,
        string address,
        int sequence,
        double? latitude = null,
        double? longitude = null,
        string? contactName = null,
        string? contactPhone = null)
    {
        ValidateTenantAndShipment(tenantId, shipmentId);
        ValidateRequiredText(name, nameof(name), NameMaxLength);
        ValidateRequiredText(address, nameof(address), AddressMaxLength);
        ValidateSequence(sequence);
        ValidateCoordinates(latitude, longitude);

        return new ShipmentLocation
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            Type = type,
            Name = name.Trim(),
            Address = address.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            ContactName = NormalizeOptionalText(contactName, ContactNameMaxLength, nameof(contactName)),
            ContactPhone = NormalizeOptionalText(contactPhone, ContactPhoneMaxLength, nameof(contactPhone)),
            Sequence = sequence
        };
    }

    public Guid ShipmentId { get; private set; }
    public Shipment? Shipment { get; private set; }
    public LocationType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactPhone { get; private set; }
    public int Sequence { get; private set; }

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

    private static void ValidateSequence(int sequence)
    {
        if (sequence <= 0)
        {
            throw new ArgumentException("Location sequence must be positive.", nameof(sequence));
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

    private static void ValidateRequiredText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{name} must be {maxLength} characters or fewer.", name);
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
