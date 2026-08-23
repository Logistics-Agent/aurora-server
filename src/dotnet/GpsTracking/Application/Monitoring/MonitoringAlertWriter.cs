using System.Text.Json;
using GpsTracking.Contracts.Events;
using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;

namespace GpsTracking.Application.Monitoring;

internal sealed class MonitoringAlertWriter(GpsTrackingDbContext dbContext)
{
    internal async Task<MonitoringAlert?> RaiseAsync(
        Guid tenantId,
        MonitoringAlertType alertType,
        string vehicleId,
        Guid? shipmentId,
        Guid? geofenceId,
        Guid? positionId,
        string message,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var key = Key(alertType, vehicleId, geofenceId);
        if (FindLocal(tenantId, key) is not null
            || await dbContext.MonitoringAlerts.IgnoreQueryFilters().AnyAsync(
                item => item.TenantId == tenantId
                    && item.DeduplicationKey == key
                    && item.Status == MonitoringAlertStatus.Active,
                cancellationToken))
        {
            return null;
        }

        var alert = MonitoringAlert.Raise(
            tenantId,
            alertType,
            vehicleId,
            shipmentId,
            geofenceId,
            positionId,
            message,
            occurredAt,
            key);
        dbContext.MonitoringAlerts.Add(alert);
        var integrationEvent = new GpsMonitoringAlertRaisedEvent
        {
            TenantId = tenantId,
            AlertId = alert.Id,
            AlertType = alert.AlertType.ToString(),
            VehicleId = alert.VehicleId,
            ShipmentId = alert.ShipmentId,
            GeofenceId = alert.GeofenceId,
            PositionId = alert.PositionId,
            Message = alert.Message,
            OccurredAt = alert.OccurredAt
        };
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            tenantId,
            integrationEvent.EventId,
            nameof(GpsMonitoringAlertRaisedEvent),
            JsonSerializer.Serialize(integrationEvent),
            occurredAt));
        return alert;
    }

    internal async Task ResolveAsync(
        Guid tenantId,
        MonitoringAlertType alertType,
        string vehicleId,
        Guid? shipmentId,
        Guid? geofenceId,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        var key = Key(alertType, vehicleId, geofenceId);
        var local = FindLocal(tenantId, key);
        if (local is not null)
        {
            local.Resolve(resolvedAt);
            return;
        }

        var alert = await dbContext.MonitoringAlerts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId
                && item.DeduplicationKey == key
                && item.Status == MonitoringAlertStatus.Active, cancellationToken);
        alert?.Resolve(resolvedAt);
    }

    private MonitoringAlert? FindLocal(Guid tenantId, string key) =>
        dbContext.MonitoringAlerts.Local.SingleOrDefault(item => item.TenantId == tenantId
            && item.DeduplicationKey == key
            && item.Status == MonitoringAlertStatus.Active);

    private static string Key(
        MonitoringAlertType alertType,
        string vehicleId,
        Guid? geofenceId) => $"{alertType}:{vehicleId}:{geofenceId}";
}
