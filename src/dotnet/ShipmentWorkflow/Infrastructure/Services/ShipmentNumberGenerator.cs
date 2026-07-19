using ShipmentWorkflow.Application.Interfaces;

namespace ShipmentWorkflow.Infrastructure.Services;

public sealed class ShipmentNumberGenerator : IShipmentNumberGenerator
{
    public string Generate()
    {
        var datePart = DateTimeOffset.UtcNow.ToString("yyyyMMdd");

        var uniquePart = Guid.CreateVersion7()
            .ToString("N")[..10]
            .ToUpperInvariant();

        return $"SHP-{datePart}-{uniquePart}";
    }
}
