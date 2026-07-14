using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Application.Queries.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Grpc;

namespace ShipmentWorkflow.GrpcServices;

public sealed class ShipmentGrpcService(ISender sender)
    : ShipmentWorkflowService.ShipmentWorkflowServiceBase
{
    public override async Task<ShipmentResponse> CreateShipment(
        CreateShipmentRequest request,
        ServerCallContext context)
    {
        var command = new CreateShipmentCommand(
            OrderId: request.OrderId,
            CustomerName: request.CustomerName,
            DestinationAddress: request.DestinationAddress,
            CargoItems: request.CargoItems
                .Select(cargoItem => new CreateShipmentCargoItem(
                    cargoItem.Name,
                    cargoItem.Quantity,
                    cargoItem.WeightKg,
                    cargoItem.HsCode))
                .ToArray());

        var shipment = await sender.Send(
            command,
            context.CancellationToken);

        return MapToResponse(shipment);
    }

    public override async Task<ShipmentResponse> GetShipment(
        GetShipmentRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var shipmentId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid shipment id."));
        }

        var shipment = await sender.Send(
            new GetShipmentQuery(shipmentId),
            context.CancellationToken);

        return MapToResponse(shipment);
    }

    public override async Task<ListShipmentsResponse> ListShipments(
        ListShipmentsRequest request,
        ServerCallContext context)
    {
        var result = await sender.Send(
            new ListShipmentsQuery(
                request.Page,
                request.Limit,
                request.Status,
                request.ShipmentNo,
                request.CustomerName,
                request.CreatedFrom?.ToDateTimeOffset(),
                request.CreatedTo?.ToDateTimeOffset()),
            context.CancellationToken);

        var response = new ListShipmentsResponse
        {
            Page = result.Page,
            Limit = result.Limit,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };

        response.Shipments.AddRange(result.Shipments.Select(MapToResponse));

        return response;
    }

    public override async Task<ShipmentTimelineResponse> GetShipmentTimeline(
        GetShipmentTimelineRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ShipmentId, out var shipmentId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid shipment id."));
        }

        var timeline = await sender.Send(
            new GetShipmentTimelineQuery(shipmentId),
            context.CancellationToken);

        var response = new ShipmentTimelineResponse
        {
            ShipmentId = timeline.ShipmentId.ToString()
        };

        response.Items.AddRange(timeline.Items.Select(item => new ShipmentTimelineItem
        {
            Status = item.Status.ToString(),
            Note = item.Note ?? string.Empty,
            CreatedAt = Timestamp.FromDateTimeOffset(item.CreatedAt),
            Source = item.Source
        }));

        return response;
    }

    public override async Task<ShipmentResponse> SubmitShipment(
        SubmitShipmentRequest request,
        ServerCallContext context)
    {
        var shipmentId = ParseGuid(request.Id, "Invalid shipment id.");
        var shipment = await sender.Send(
            new SubmitShipmentCommand(shipmentId),
            context.CancellationToken);

        return MapToResponse(shipment);
    }

    public override async Task<ShipmentResponse> UpdateShipment(
        UpdateShipmentRequest request,
        ServerCallContext context)
    {
        var shipmentId = ParseGuid(request.Id, "Invalid shipment id.");
        var shipment = await sender.Send(
            new UpdateShipmentCommand(
                shipmentId,
                request.CustomerName,
                request.DestinationAddress,
                ParseEnum<ShipmentPriority>(request.Priority, ShipmentPriority.Normal),
                ParseEnum<TransportMode>(request.TransportMode, TransportMode.Unknown),
                request.Notes),
            context.CancellationToken);

        return MapToResponse(shipment);
    }

    public override async Task<ShipmentResponse> UpdateShipmentStatus(
        UpdateShipmentStatusRequest request,
        ServerCallContext context)
    {
        var shipmentId = ParseGuid(request.Id, "Invalid shipment id.");
        if (!System.Enum.TryParse<ShipmentStatus>(request.Status, ignoreCase: true, out var status))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid shipment status."));
        }

        var shipment = await sender.Send(
            new UpdateShipmentStatusCommand(shipmentId, status, request.Note),
            context.CancellationToken);

        return MapToResponse(shipment);
    }

    public override async Task<ShipmentResponse> CancelShipment(
        CancelShipmentRequest request,
        ServerCallContext context)
    {
        var shipmentId = ParseGuid(request.Id, "Invalid shipment id.");
        var shipment = await sender.Send(
            new CancelShipmentCommand(shipmentId, request.Reason),
            context.CancellationToken);

        return MapToResponse(shipment);
    }

    public override async Task<DeleteDraftShipmentResponse> DeleteDraftShipment(
        DeleteDraftShipmentRequest request,
        ServerCallContext context)
    {
        var shipmentId = ParseGuid(request.Id, "Invalid shipment id.");
        await sender.Send(
            new DeleteDraftShipmentCommand(shipmentId),
            context.CancellationToken);

        return new DeleteDraftShipmentResponse { Deleted = true };
    }

    private static Guid ParseGuid(string value, string errorMessage)
    {
        return Guid.TryParse(value, out var id)
            ? id
            : throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return System.Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid {typeof(TEnum).Name}."));
    }

    private static ShipmentResponse MapToResponse(ShipmentDto shipment)
    {
        var response = new ShipmentResponse
        {
            Id = shipment.Id.ToString(),
            TenantId = shipment.TenantId.ToString(),
            ShipmentNo = shipment.ShipmentNo,
            OrderId = shipment.OrderId ?? string.Empty,
            CustomerId = shipment.CustomerId?.ToString() ?? string.Empty,
            CustomerName = shipment.CustomerName,
            DestinationAddress = shipment.DestinationAddress,
            Status = shipment.Status.ToString(),
            Priority = shipment.Priority.ToString(),
            TransportMode = shipment.TransportMode.ToString(),
            RouteId = shipment.RouteId ?? string.Empty,
            VehicleId = shipment.VehicleId ?? string.Empty,
            Notes = shipment.Notes ?? string.Empty,
            CreatedAt = Timestamp.FromDateTimeOffset(shipment.CreatedAt)
        };

        SetOptionalTimestamp(response, shipment);

        response.CargoItems.AddRange(shipment.CargoItems.Select(cargoItem =>
            new CargoItemResponse
            {
                Id = cargoItem.Id.ToString(),
                Name = cargoItem.Name,
                Quantity = cargoItem.Quantity,
                WeightKg = cargoItem.WeightKg,
                HsCode = cargoItem.HsCode ?? string.Empty
            }));

        response.Locations.AddRange(shipment.Locations.Select(location =>
            new ShipmentLocationResponse
            {
                Id = location.Id.ToString(),
                Type = location.Type.ToString(),
                Name = location.Name,
                Address = location.Address,
                Latitude = location.Latitude ?? 0,
                Longitude = location.Longitude ?? 0,
                ContactName = location.ContactName ?? string.Empty,
                ContactPhone = location.ContactPhone ?? string.Empty,
                Sequence = location.Sequence
            }));

        response.Documents.AddRange(shipment.Documents.Select(document =>
            new ShipmentDocumentResponse
            {
                Id = document.Id.ToString(),
                FileName = document.FileName,
                DocumentType = document.DocumentType.ToString(),
                StorageUrl = document.StorageUrl,
                OcrStatus = document.OCRStatus.ToString(),
                OcrConfidence = document.OCRConfidence.HasValue
                    ? (double)document.OCRConfidence.Value
                    : 0,
                UploadedBy = document.UploadedBy?.ToString() ?? string.Empty,
                UploadedAt = Timestamp.FromDateTimeOffset(document.UploadedAt),
                ExtractedDataJson = document.ExtractedDataJson ?? string.Empty
            }));

        response.Milestones.AddRange(shipment.Milestones.Select(milestone =>
            new ShipmentMilestoneResponse
            {
                Id = milestone.Id.ToString(),
                Status = milestone.Status.ToString(),
                Description = milestone.Description ?? string.Empty,
                Latitude = milestone.Latitude ?? 0,
                Longitude = milestone.Longitude ?? 0,
                RecordedAt = Timestamp.FromDateTimeOffset(milestone.RecordedAt),
                Source = milestone.Source.ToString(),
                CreatedBy = milestone.CreatedByUserId?.ToString() ?? string.Empty
            }));

        return response;
    }

    private static void SetOptionalTimestamp(
        ShipmentResponse response,
        ShipmentDto shipment)
    {
        if (shipment.UpdatedAt.HasValue)
        {
            response.UpdatedAt = Timestamp.FromDateTimeOffset(shipment.UpdatedAt.Value);
        }

        if (shipment.EstimatedPickupTime.HasValue)
        {
            response.EstimatedPickupTime = Timestamp.FromDateTimeOffset(shipment.EstimatedPickupTime.Value);
        }

        if (shipment.EstimatedDeliveryTime.HasValue)
        {
            response.EstimatedDeliveryTime = Timestamp.FromDateTimeOffset(shipment.EstimatedDeliveryTime.Value);
        }

        if (shipment.ActualPickupTime.HasValue)
        {
            response.ActualPickupTime = Timestamp.FromDateTimeOffset(shipment.ActualPickupTime.Value);
        }

        if (shipment.ActualDeliveryTime.HasValue)
        {
            response.ActualDeliveryTime = Timestamp.FromDateTimeOffset(shipment.ActualDeliveryTime.Value);
        }
    }
}
