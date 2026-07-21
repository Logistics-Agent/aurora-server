using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;

namespace GpsTracking.Application.Queries;

public sealed record LocationSelector(string? VehicleId, Guid? ShipmentId);

public sealed record LocationHistoryPage(
    IReadOnlyList<GpsPosition> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public interface ILocationQueryService
{
    Task<CurrentLocation> GetCurrentAsync(
        LocationSelector selector,
        CancellationToken cancellationToken = default);

    Task<LocationHistoryPage> ListHistoryAsync(
        LocationSelector selector,
        DateTimeOffset from,
        DateTimeOffset to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public sealed class LocationQueryService(
    GpsTrackingDbContext dbContext,
    ICurrentUserService currentUser) : ILocationQueryService
{
    public async Task<CurrentLocation> GetCurrentAsync(
        LocationSelector selector,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var normalized = Normalize(selector);
        var query = dbContext.CurrentLocations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        query = normalized.VehicleId is not null
            ? query.Where(item => item.VehicleId == normalized.VehicleId)
            : query.Where(item => item.ShipmentId == normalized.ShipmentId);

        return await query
            .OrderByDescending(item => item.RecordedAt)
            .ThenByDescending(item => item.PositionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Current location was not found.");
    }

    public async Task<LocationHistoryPage> ListHistoryAsync(
        LocationSelector selector,
        DateTimeOffset from,
        DateTimeOffset to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var normalized = Normalize(selector);
        if (from == default || to == default || from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero)
            throw new DomainException("History range must contain UTC from and to timestamps.");
        if (from >= to)
            throw new DomainException("History from must precede to.");
        if (to - from > TimeSpan.FromDays(7))
            throw new DomainException("History range cannot exceed seven days.");
        if (page < 1)
            throw new DomainException("Page must be at least 1.");
        if (pageSize is < 1 or > 500)
            throw new DomainException("Page size must be between 1 and 500.");
        if (page > int.MaxValue / pageSize)
            throw new DomainException("Page number is too large.");

        var query = dbContext.Positions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.RecordedAt >= from
                && item.RecordedAt <= to);
        query = normalized.VehicleId is not null
            ? query.Where(item => item.VehicleId == normalized.VehicleId)
            : query.Where(item => item.ShipmentId == normalized.ShipmentId);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.RecordedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new LocationHistoryPage(
            items,
            page,
            pageSize,
            totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    private Guid RequireTenant() =>
        currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new DomainException("TenantId was not found in the authenticated user context.");

    private static LocationSelector Normalize(LocationSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var vehicleId = string.IsNullOrWhiteSpace(selector.VehicleId)
            ? null
            : selector.VehicleId.Trim();
        if (selector.ShipmentId == Guid.Empty)
            throw new DomainException("ShipmentId cannot be empty.");
        if ((vehicleId is null) == !selector.ShipmentId.HasValue)
            throw new DomainException("Exactly one vehicle or shipment selector is required.");
        return new LocationSelector(vehicleId, selector.ShipmentId);
    }
}
