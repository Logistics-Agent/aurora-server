using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GpsTracking.Application.Ingestion;
using GpsTracking.Domain.Entities;
using Shared.Security;
using GpsGrpc = GpsTracking.Grpc;

namespace GpsTracking.GrpcServices;

public sealed class GpsTrackingGrpcService(
    IPositionIngestionService ingestionService,
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

    private void RequireTenant()
    {
        if (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Tenant context is required."));
    }

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));
}
