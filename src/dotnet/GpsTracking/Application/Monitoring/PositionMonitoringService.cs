using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;

namespace GpsTracking.Application.Monitoring;

public interface IPositionMonitoringService
{
    Task EvaluateAsync(
        GpsPosition position,
        CurrentLocation current,
        CancellationToken cancellationToken = default);
}

public sealed class PositionMonitoringService : IPositionMonitoringService
{
    private readonly GpsTrackingDbContext _dbContext;
    private readonly MonitoringOptions _options;
    private readonly MonitoringAlertWriter _alerts;

    public PositionMonitoringService(GpsTrackingDbContext dbContext, MonitoringOptions options)
    {
        options.Validate();
        _dbContext = dbContext;
        _options = options;
        _alerts = new MonitoringAlertWriter(dbContext);
    }

    public async Task EvaluateAsync(
        GpsPosition position,
        CurrentLocation current,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(current);
        await _alerts.ResolveAsync(
            position.TenantId,
            MonitoringAlertType.SignalLost,
            position.VehicleId,
            position.ShipmentId,
            null,
            position.ReceivedAt,
            cancellationToken);
        await EvaluateAbnormalStop(position, current, cancellationToken);
        await EvaluateGeofences(position, cancellationToken);
    }

    private async Task EvaluateAbnormalStop(
        GpsPosition position,
        CurrentLocation current,
        CancellationToken cancellationToken)
    {
        if (position.SpeedKph is not null && position.SpeedKph <= _options.StationarySpeedKph
            && current.StationarySince.HasValue
            && position.ReceivedAt - current.StationarySince.Value >= _options.AbnormalStopDuration)
        {
            await _alerts.RaiseAsync(
                position.TenantId,
                MonitoringAlertType.AbnormalStop,
                position.VehicleId,
                position.ShipmentId,
                null,
                position.Id,
                "Vehicle has remained stopped beyond the configured threshold.",
                position.ReceivedAt,
                cancellationToken);
            return;
        }

        await _alerts.ResolveAsync(
            position.TenantId,
            MonitoringAlertType.AbnormalStop,
            position.VehicleId,
            position.ShipmentId,
            null,
            position.ReceivedAt,
            cancellationToken);
    }

    private async Task EvaluateGeofences(
        GpsPosition position,
        CancellationToken cancellationToken)
    {
        var geofences = await _dbContext.Geofences
            .Where(item => item.TenantId == position.TenantId
                && item.IsActive
                && (item.VehicleId == null || item.VehicleId == position.VehicleId)
                && (item.ShipmentId == null || item.ShipmentId == position.ShipmentId))
            .ToListAsync(cancellationToken);

        foreach (var geofence in geofences)
        {
            var isInside = GeofenceDistanceCalculator.DistanceMeters(
                position.Latitude,
                position.Longitude,
                geofence.Latitude,
                geofence.Longitude) <= geofence.RadiusMeters;
            var presence = _dbContext.GeofencePresences.Local.SingleOrDefault(item =>
                    item.TenantId == position.TenantId
                    && item.GeofenceId == geofence.Id
                    && item.VehicleId == position.VehicleId)
                ?? await _dbContext.GeofencePresences.SingleOrDefaultAsync(item =>
                    item.TenantId == position.TenantId
                    && item.GeofenceId == geofence.Id
                    && item.VehicleId == position.VehicleId, cancellationToken);

            var changed = false;
            if (presence is null)
            {
                presence = GeofencePresence.Create(
                    position.TenantId, geofence.Id, position.VehicleId, isInside, position.RecordedAt);
                _dbContext.GeofencePresences.Add(presence);
                changed = isInside;
            }
            else
            {
                changed = presence.Observe(isInside, position.RecordedAt);
            }
            if (!changed)
                continue;

            var activeType = isInside
                ? MonitoringAlertType.GeofenceEntered
                : MonitoringAlertType.GeofenceExited;
            var resolvedType = isInside
                ? MonitoringAlertType.GeofenceExited
                : MonitoringAlertType.GeofenceEntered;
            await _alerts.ResolveAsync(
                position.TenantId,
                resolvedType,
                position.VehicleId,
                position.ShipmentId,
                geofence.Id,
                position.ReceivedAt,
                cancellationToken);
            await _alerts.RaiseAsync(
                position.TenantId,
                activeType,
                position.VehicleId,
                position.ShipmentId,
                geofence.Id,
                position.Id,
                isInside ? $"Vehicle entered geofence {geofence.Name}." : $"Vehicle exited geofence {geofence.Name}.",
                position.ReceivedAt,
                cancellationToken);
        }
    }
}
