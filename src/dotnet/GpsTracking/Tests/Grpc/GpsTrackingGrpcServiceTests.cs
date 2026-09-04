using Grpc.Core;
using GpsTracking.Application.Ingestion;
using GpsTracking.Application.Monitoring;
using GpsTracking.Application.Queries;
using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Grpc;
using GpsTracking.GrpcServices;
using Shared.Security;

namespace GpsTracking.Tests.Grpc;

public sealed class GpsTrackingGrpcServiceTests
{
    [Fact]
    public async Task IngestRejectsMissingTenantAsUnauthenticated()
    {
        var service = new GpsTrackingGrpcService(
            new StubIngestionService(_ => throw new InvalidOperationException()),
            new StubLocationQueryService(),
            new StubMonitoringManagementService(),
            new CurrentUserService());

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.IngestPosition(new IngestPositionRequest(), TestServerCallContext.Create()));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task IngestMapsDomainValidationToInvalidArgument()
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, [], []);
        currentUser.Populate(Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, []);
        var service = new GpsTrackingGrpcService(
            new StubIngestionService(_ => throw new ArgumentOutOfRangeException("latitude")),
            new StubLocationQueryService(),
            new StubMonitoringManagementService(),
            currentUser);
        var request = new IngestPositionRequest
        {
            ExternalReadingId = "reading",
            DeviceId = "device",
            VehicleId = "vehicle",
            RecordedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.IngestPosition(request, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private sealed class StubIngestionService(
        Func<IngestPositionInput, GpsPosition> handler) : IPositionIngestionService
    {
        public Task<GpsPosition> IngestAsync(
            IngestPositionInput input,
            CancellationToken cancellationToken = default) => Task.FromResult(handler(input));
    }

    private sealed class StubLocationQueryService : ILocationQueryService
    {
        public Task<CurrentLocation> GetCurrentAsync(
            LocationSelector selector,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LocationHistoryPage> ListHistoryAsync(
            LocationSelector selector,
            DateTimeOffset from,
            DateTimeOffset to,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubMonitoringManagementService : IMonitoringManagementService
    {
        public Task<Geofence> CreateGeofenceAsync(
            CreateGeofenceInput input,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Geofence>> ListGeofencesAsync(
            bool includeInactive,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Geofence> SetGeofenceActiveAsync(
            Guid geofenceId,
            bool isActive,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MonitoringAlertPage> ListAlertsAsync(
            MonitoringAlertType? alertType,
            MonitoringAlertStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MonitoringAlert> ResolveAlertAsync(
            Guid alertId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
