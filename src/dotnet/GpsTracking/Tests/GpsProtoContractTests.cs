using GpsTracking.Grpc;

namespace GpsTracking.Tests;

public sealed class GpsProtoContractTests
{
    [Fact]
    public void IngestionContractDoesNotAcceptTenantOrShipmentIdentity()
    {
        var fields = IngestPositionRequest.Descriptor.Fields.InFieldNumberOrder()
            .Select(field => field.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("tenant_id", fields);
        Assert.DoesNotContain("shipment_id", fields);
    }
}
