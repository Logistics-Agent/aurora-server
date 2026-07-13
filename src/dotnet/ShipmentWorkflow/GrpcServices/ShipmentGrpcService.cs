using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Application.DTOs.Shipments;
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

    public override Task<ShipmentResponse> GetShipment(
        GetShipmentRequest request,
        ServerCallContext context)
    {
        throw new RpcException(
            new Status(
                StatusCode.Unimplemented,
                "GetShipment is not implemented yet."));
    }

    public override Task<ShipmentResponse> UpdateShipmentStatus(
        UpdateShipmentStatusRequest request,
        ServerCallContext context)
    {
        throw new RpcException(
            new Status(
                StatusCode.Unimplemented,
                "UpdateShipmentStatus is not implemented yet."));
    }

    private static ShipmentResponse MapToResponse(ShipmentDto shipment)
    {
        var response = new ShipmentResponse
        {
            Id = shipment.Id.ToString(),
            TenantId = shipment.TenantId.ToString(),
            ShipmentNo = shipment.ShipmentNo,
            OrderId = shipment.OrderId ?? string.Empty,
            CustomerName = shipment.CustomerName,
            DestinationAddress = shipment.DestinationAddress,
            Status = shipment.Status.ToString(),
            CreatedAt = Timestamp.FromDateTime(
                shipment.CreatedAt.UtcDateTime)
        };

        if (shipment.UpdatedAt.HasValue)
        {
            response.UpdatedAt = Timestamp.FromDateTime(
                shipment.UpdatedAt.Value.UtcDateTime);
        }

        response.CargoItems.AddRange(shipment.CargoItems.Select(cargoItem =>
            new CargoItemResponse
            {
                Id = cargoItem.Id.ToString(),
                Name = cargoItem.Name,
                Quantity = cargoItem.Quantity,
                WeightKg = cargoItem.WeightKg,
                HsCode = cargoItem.HsCode ?? string.Empty
            }));

        return response;
    }
}