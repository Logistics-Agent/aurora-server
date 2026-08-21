using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;

namespace GpsTracking.Application.Monitoring;

public sealed record CreateGeofenceInput(
    string Name,
    decimal Latitude,
    decimal Longitude,
    decimal RadiusMeters,
    Guid? ShipmentId,
    string? VehicleId);

public sealed record MonitoringAlertPage(
    IReadOnlyList<MonitoringAlert> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public interface IMonitoringManagementService
{
    Task<Geofence> CreateGeofenceAsync(CreateGeofenceInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Geofence>> ListGeofencesAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<Geofence> SetGeofenceActiveAsync(Guid geofenceId, bool isActive, CancellationToken cancellationToken = default);
    Task<MonitoringAlertPage> ListAlertsAsync(
        MonitoringAlertType? alertType,
        MonitoringAlertStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<MonitoringAlert> ResolveAlertAsync(Guid alertId, CancellationToken cancellationToken = default);
}

public sealed class MonitoringManagementService(
    GpsTrackingDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IMonitoringManagementService
{
    public async Task<Geofence> CreateGeofenceAsync(
        CreateGeofenceInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var geofence = Geofence.Create(
            RequireTenant(), input.Name, input.Latitude, input.Longitude,
            input.RadiusMeters, input.ShipmentId, input.VehicleId);
        dbContext.Geofences.Add(geofence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return geofence;
    }

    public async Task<IReadOnlyList<Geofence>> ListGeofencesAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var query = dbContext.Geofences.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(item => item.IsActive);
        return await query.OrderBy(item => item.Name).ThenBy(item => item.Id).Take(1_000).ToListAsync(cancellationToken);
    }

    public async Task<Geofence> SetGeofenceActiveAsync(
        Guid geofenceId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        if (geofenceId == Guid.Empty)
            throw new DomainException("GeofenceId is required.");
        var geofence = await dbContext.Geofences.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == geofenceId,
            cancellationToken) ?? throw new NotFoundException("Geofence was not found.");
        geofence.SetActive(isActive, timeProvider.GetUtcNow());
        if (!isActive)
        {
            var presences = await dbContext.GeofencePresences
                .Where(item => item.TenantId == tenantId && item.GeofenceId == geofenceId)
                .ToListAsync(cancellationToken);
            dbContext.GeofencePresences.RemoveRange(presences);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return geofence;
    }

    public async Task<MonitoringAlertPage> ListAlertsAsync(
        MonitoringAlertType? alertType,
        MonitoringAlertStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new DomainException("Alert paging is invalid; page starts at 1 and page size is 1-100.");
        if (alertType.HasValue && !Enum.IsDefined(alertType.Value))
            throw new DomainException("Alert type is invalid.");
        if (status.HasValue && !Enum.IsDefined(status.Value))
            throw new DomainException("Alert status is invalid.");

        var query = dbContext.MonitoringAlerts.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (alertType.HasValue)
            query = query.Where(item => item.AlertType == alertType.Value);
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new MonitoringAlertPage(
            items, page, pageSize, totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<MonitoringAlert> ResolveAlertAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        if (alertId == Guid.Empty)
            throw new DomainException("AlertId is required.");
        var alert = await dbContext.MonitoringAlerts.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == alertId,
            cancellationToken) ?? throw new NotFoundException("Monitoring alert was not found.");
        alert.Resolve(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return alert;
    }

    private Guid RequireTenant() =>
        currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new DomainException("TenantId was not found in the authenticated user context.");
}
