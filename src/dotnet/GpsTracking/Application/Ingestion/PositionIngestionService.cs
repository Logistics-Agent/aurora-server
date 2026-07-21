using System.Text.Json;
using GpsTracking.Contracts.Events;
using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.Persistences;
using GpsTracking.Application.Monitoring;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Exceptions;
using Shared.Security;

namespace GpsTracking.Application.Ingestion;

public sealed record IngestPositionInput(
    string ExternalReadingId,
    string DeviceId,
    string VehicleId,
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKph,
    decimal? HeadingDegrees,
    decimal? AccuracyMeters,
    DateTimeOffset RecordedAt);

public interface IPositionIngestionService
{
    Task<GpsPosition> IngestAsync(
        IngestPositionInput input,
        CancellationToken cancellationToken = default);
}

public sealed class PositionIngestionService(
    GpsTrackingDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    MonitoringOptions monitoringOptions,
    IPositionMonitoringService monitoringService) : IPositionIngestionService
{
    public async Task<GpsPosition> IngestAsync(
        IngestPositionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = currentUser.TenantId is { } id && id != Guid.Empty
            ? id
            : throw new DomainException("TenantId was not found in the authenticated user context.");
        var deviceId = Required(input.DeviceId, nameof(input.DeviceId));
        var vehicleId = Required(input.VehicleId, nameof(input.VehicleId));
        var externalReadingId = Required(input.ExternalReadingId, nameof(input.ExternalReadingId));

        var existing = await dbContext.Positions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId
                && item.DeviceId == deviceId
                && item.ExternalReadingId == externalReadingId,
            cancellationToken);
        if (existing is not null)
            return existing;

        var assignment = await dbContext.VehicleShipmentAssignments
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.VehicleId == vehicleId
                && item.EndedAt == null)
            .OrderByDescending(item => item.AssignedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var receivedAt = timeProvider.GetUtcNow();
        var position = GpsPosition.Create(
            tenantId,
            deviceId,
            vehicleId,
            assignment?.ShipmentId,
            externalReadingId,
            input.Latitude,
            input.Longitude,
            input.SpeedKph,
            input.HeadingDegrees,
            input.AccuracyMeters,
            input.RecordedAt,
            receivedAt);
        dbContext.Positions.Add(position);

        var currentLocation = await dbContext.CurrentLocations.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.VehicleId == position.VehicleId,
            cancellationToken);
        var advancesCurrent = currentLocation is null;
        if (currentLocation is null)
        {
            currentLocation = CurrentLocation.FromPosition(
                position, monitoringOptions.StationarySpeedKph);
            dbContext.CurrentLocations.Add(currentLocation);
        }
        else
        {
            advancesCurrent = currentLocation.Apply(
                position, monitoringOptions.StationarySpeedKph);
        }
        if (advancesCurrent)
            await monitoringService.EvaluateAsync(position, currentLocation, cancellationToken);

        var integrationEvent = new GpsPositionUpdatedEvent
        {
            TenantId = tenantId,
            PositionId = position.Id,
            DeviceId = position.DeviceId,
            VehicleId = position.VehicleId,
            ShipmentId = position.ShipmentId,
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            SpeedKph = position.SpeedKph,
            HeadingDegrees = position.HeadingDegrees,
            RecordedAt = position.RecordedAt
        };
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            tenantId,
            integrationEvent.EventId,
            nameof(GpsPositionUpdatedEvent),
            JsonSerializer.Serialize(integrationEvent),
            receivedAt));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return position;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var duplicate = await dbContext.Positions.SingleOrDefaultAsync(
                    item => item.TenantId == tenantId
                        && item.DeviceId == deviceId
                        && item.ExternalReadingId == externalReadingId,
                    cancellationToken);
            if (duplicate is not null)
                return duplicate;
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static string Required(string? value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException($"{parameterName} is required.", parameterName);
}
