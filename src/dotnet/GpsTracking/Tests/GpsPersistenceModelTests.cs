using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Tests;

public sealed class GpsPersistenceModelTests
{
    private static readonly Type[] TenantEntityTypes =
    [
        typeof(GpsPosition), typeof(CurrentLocation), typeof(VehicleShipmentAssignment),
        typeof(ShipmentTrackingState),
        typeof(Geofence), typeof(GeofencePresence), typeof(MonitoringAlert),
        typeof(ConsumedIntegrationEvent), typeof(OutboxMessage)
    ];

    [Fact]
    public void ModelDefinesTenantFiltersAndOperationalIndexes()
    {
        using var context = CreateContext(new CurrentUserService());

        Assert.All(TenantEntityTypes, type =>
            Assert.NotEmpty(context.Model.FindEntityType(type)!.GetDeclaredQueryFilters()));

        AssertIndex(context, typeof(GpsPosition), true, "TenantId", "DeviceId", "ExternalReadingId");
        AssertIndex(context, typeof(CurrentLocation), true, "TenantId", "VehicleId");
        AssertIndex(context, typeof(GeofencePresence), true, "TenantId", "GeofenceId", "VehicleId");
        AssertIndex(context, typeof(ConsumedIntegrationEvent), true, "SourceEventType", "SourceEventId");
        AssertIndex(context, typeof(OutboxMessage), true, "EventId");
    }

    [Fact]
    public void ModelUsesCoordinatePrecisionAndConservativeRelationships()
    {
        using var context = CreateContext(new CurrentUserService());
        var position = context.Model.FindEntityType(typeof(GpsPosition))!;

        Assert.Equal(9, position.FindProperty(nameof(GpsPosition.Latitude))!.GetPrecision());
        Assert.Equal(6, position.FindProperty(nameof(GpsPosition.Latitude))!.GetScale());
        Assert.Equal(DeleteBehavior.Cascade,
            context.Model.FindEntityType(typeof(GeofencePresence))!.GetForeignKeys().Single().DeleteBehavior);
    }

    [Fact]
    public async Task MissingTenantContextNeverDisablesFilters()
    {
        var databaseName = $"gps-model-{Guid.CreateVersion7()}";
        var tenant = new CurrentUserService();
        var tenantId = Guid.CreateVersion7();
        tenant.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        tenant.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);

        await using (var writeContext = CreateContext(tenant, databaseName))
        {
            writeContext.Positions.Add(GpsPosition.Create(
                tenantId, "device", "vehicle", null, "reading", 10, 106,
                20, 90, 5, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow));
            await writeContext.SaveChangesAsync();
        }

        await using var missingTenantContext = CreateContext(new CurrentUserService(), databaseName);

        Assert.Empty(await missingTenantContext.Positions.ToListAsync());
    }

    private static GpsTrackingDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<GpsTrackingDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new GpsTrackingDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static void AssertIndex(
        DbContext context, Type entityType, bool unique, params string[] propertyNames)
    {
        var index = context.Model.FindEntityType(entityType)!.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.NotNull(index);
        Assert.Equal(unique, index!.IsUnique);
    }
}
