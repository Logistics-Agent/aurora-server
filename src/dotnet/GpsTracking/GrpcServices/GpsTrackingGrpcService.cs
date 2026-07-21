using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GpsTracking.Application.Ingestion;
using GpsTracking.Application.Queries;
using GpsTracking.Domain.Entities;
using Shared.Security;
using GpsGrpc = GpsTracking.Grpc;

namespace GpsTracking.GrpcServices;

public sealed class GpsTrackingGrpcService(
    IPositionIngestionService ingestionService,
    ILocationQueryService locationQueries,
    ICurrentUserService currentUser)
    : GpsGrpc.GpsTrackingService.GpsTrackingServiceBase
{
    public override async Task<GpsGrpc.PositionResponse> IngestPosition(
        GpsGrpc.IngestPositionRequest request,
        ServerCallContext context)
    {
        RequireTenant();
        if (request.RecordedAt is null)
            throw InvalidArgument("RecordedAt is required.");

        try
        {
            var position = await ingestionService.IngestAsync(
                new IngestPositionInput(
                    request.ExternalReadingId,
                    request.DeviceId,
                    request.VehicleId,
                    Convert.ToDecimal(request.Latitude),
                    Convert.ToDecimal(request.Longitude),
                    request.HasSpeedKph ? Convert.ToDecimal(request.SpeedKph) : null,
                    request.HasHeadingDegrees ? Convert.ToDecimal(request.HeadingDegrees) : null,
                    request.HasAccuracyMeters ? Convert.ToDecimal(request.AccuracyMeters) : null,
                    request.RecordedAt.ToDateTimeOffset()),
                context.CancellationToken);
            return MapPosition(position);
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
        catch (OverflowException)
        {
            throw InvalidArgument("Position contains a number outside the supported range.");
        }
    }

    public override async Task<GpsGrpc.CurrentLocationResponse> GetCurrentLocation(
        GpsGrpc.GetCurrentLocationRequest request,
        ServerCallContext context)
    {
        RequireTenant();
        var current = await locationQueries.GetCurrentAsync(
            ParseSelector(request.SelectorCase, request.VehicleId, request.ShipmentId),
            context.CancellationToken);
        return MapCurrent(current);
    }

    public override async Task<GpsGrpc.ListPositionHistoryResponse> ListPositionHistory(
        GpsGrpc.ListPositionHistoryRequest request,
        ServerCallContext context)
    {
        RequireTenant();
        if (request.From is null || request.To is null)
            throw InvalidArgument("History from and to timestamps are required.");

        try
        {
            var page = await locationQueries.ListHistoryAsync(
                ParseSelector(request.SelectorCase, request.VehicleId, request.ShipmentId),
                request.From.ToDateTimeOffset(),
                request.To.ToDateTimeOffset(),
                request.Page,
                request.PageSize,
                context.CancellationToken);
            var response = new GpsGrpc.ListPositionHistoryResponse
            {
                Page = page.Page,
                PageSize = page.PageSize,
                TotalItems = page.TotalItems,
                TotalPages = page.TotalPages
            };
            response.Positions.AddRange(page.Items.Select(MapPosition));
            return response;
        }
        catch (FormatException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    internal static GpsGrpc.PositionResponse MapPosition(GpsPosition position)
    {
        var response = new GpsGrpc.PositionResponse
        {
            Id = position.Id.ToString(),
            ExternalReadingId = position.ExternalReadingId,
            DeviceId = position.DeviceId,
            VehicleId = position.VehicleId,
            ShipmentId = position.ShipmentId?.ToString() ?? string.Empty,
            Latitude = Convert.ToDouble(position.Latitude),
            Longitude = Convert.ToDouble(position.Longitude),
            RecordedAt = Timestamp.FromDateTimeOffset(position.RecordedAt),
            ReceivedAt = Timestamp.FromDateTimeOffset(position.ReceivedAt)
        };
        if (position.SpeedKph.HasValue)
            response.SpeedKph = Convert.ToDouble(position.SpeedKph.Value);
        if (position.HeadingDegrees.HasValue)
            response.HeadingDegrees = Convert.ToDouble(position.HeadingDegrees.Value);
        if (position.AccuracyMeters.HasValue)
            response.AccuracyMeters = Convert.ToDouble(position.AccuracyMeters.Value);
        return response;
    }

    internal static GpsGrpc.CurrentLocationResponse MapCurrent(CurrentLocation current)
    {
        var response = new GpsGrpc.CurrentLocationResponse
        {
            PositionId = current.PositionId.ToString(),
            VehicleId = current.VehicleId,
            ShipmentId = current.ShipmentId?.ToString() ?? string.Empty,
            Latitude = Convert.ToDouble(current.Latitude),
            Longitude = Convert.ToDouble(current.Longitude),
            RecordedAt = Timestamp.FromDateTimeOffset(current.RecordedAt),
            ReceivedAt = Timestamp.FromDateTimeOffset(current.ReceivedAt)
        };
        if (current.SpeedKph.HasValue)
            response.SpeedKph = Convert.ToDouble(current.SpeedKph.Value);
        if (current.HeadingDegrees.HasValue)
            response.HeadingDegrees = Convert.ToDouble(current.HeadingDegrees.Value);
        if (current.AccuracyMeters.HasValue)
            response.AccuracyMeters = Convert.ToDouble(current.AccuracyMeters.Value);
        return response;
    }

    private static LocationSelector ParseSelector<TSelector>(
        TSelector selectorCase,
        string vehicleId,
        string shipmentId)
        where TSelector : struct, System.Enum
    {
        var name = selectorCase.ToString();
        if (name.Equals("VehicleId", StringComparison.Ordinal))
            return new LocationSelector(vehicleId, null);
        if (name.Equals("ShipmentId", StringComparison.Ordinal)
            && Guid.TryParse(shipmentId, out var parsedShipmentId))
        {
            return new LocationSelector(null, parsedShipmentId);
        }
        throw InvalidArgument("Exactly one valid vehicle or shipment selector is required.");
    }

    private void RequireTenant()
    {
        if (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Tenant context is required."));
    }

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));
}
