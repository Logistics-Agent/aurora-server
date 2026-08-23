using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;

namespace GpsTracking.Application.Monitoring;

public sealed class SignalLossMonitor
{
    private readonly GpsTrackingDbContext _dbContext;
    private readonly MonitoringOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly MonitoringAlertWriter _alerts;

    public SignalLossMonitor(
        GpsTrackingDbContext dbContext,
        MonitoringOptions options,
        TimeProvider timeProvider)
    {
        options.Validate();
        _dbContext = dbContext;
        _options = options;
        _timeProvider = timeProvider;
        _alerts = new MonitoringAlertWriter(dbContext);
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now - _options.SignalLossThreshold;
        var assignments = await _dbContext.VehicleShipmentAssignments
            .IgnoreQueryFilters()
            .Where(item => item.EndedAt == null)
            .Where(assignment => !_dbContext.MonitoringAlerts.IgnoreQueryFilters().Any(alert =>
                alert.TenantId == assignment.TenantId
                && alert.VehicleId == assignment.VehicleId
                && alert.AlertType == MonitoringAlertType.SignalLost
                && alert.Status == MonitoringAlertStatus.Active))
            .Select(assignment => new
            {
                Assignment = assignment,
                LastSignalAt = _dbContext.CurrentLocations.IgnoreQueryFilters()
                    .Where(current => current.TenantId == assignment.TenantId
                        && current.VehicleId == assignment.VehicleId)
                    .Select(current => (DateTimeOffset?)current.RecordedAt)
                    .FirstOrDefault()
            })
            .Where(candidate => (candidate.LastSignalAt ?? candidate.Assignment.AssignedAt) <= cutoff)
            .OrderBy(candidate => candidate.LastSignalAt ?? candidate.Assignment.AssignedAt)
            .ThenBy(candidate => candidate.Assignment.Id)
            .Take(_options.SignalLossBatchSize)
            .Select(candidate => candidate.Assignment)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var vehicleIds = assignments.Select(item => item.VehicleId).Distinct().ToList();
        var currentLocations = await _dbContext.CurrentLocations
            .IgnoreQueryFilters()
            .Where(item => vehicleIds.Contains(item.VehicleId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var currentByVehicle = currentLocations.ToDictionary(
            item => (item.TenantId, item.VehicleId));
        var raised = 0;

        foreach (var assignment in assignments)
        {
            currentByVehicle.TryGetValue((assignment.TenantId, assignment.VehicleId), out var current);
            var alert = await _alerts.RaiseAsync(
                assignment.TenantId,
                MonitoringAlertType.SignalLost,
                assignment.VehicleId,
                assignment.ShipmentId,
                null,
                current?.PositionId,
                "GPS signal has exceeded the configured silence threshold.",
                now,
                cancellationToken);
            if (alert is not null)
                raised++;
        }

        if (_dbContext.ChangeTracker.HasChanges())
            await _dbContext.SaveChangesAsync(cancellationToken);
        return raised;
    }
}
