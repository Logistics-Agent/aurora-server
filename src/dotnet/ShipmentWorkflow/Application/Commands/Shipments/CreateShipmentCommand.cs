using MediatR;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Application.Interfaces;
using ShipmentWorkflow.Domain.Entities;
using ShipmentEntity =
    global::ShipmentWorkflow.Domain.Entities.Shipment;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record CreateShipmentCommand(
    string? OrderId,
    string CustomerName,
    string DestinationAddress
) : IRequest<ShipmentDto>;

public sealed class CreateShipmentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IShipmentNumberGenerator shipmentNumberGenerator)
    : IRequestHandler<CreateShipmentCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(
        CreateShipmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException(
                "TenantId was not found in the authenticated user context.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new ArgumentException(
                "Customer name is required.",
                nameof(request.CustomerName));
        }

        if (string.IsNullOrWhiteSpace(request.DestinationAddress))
        {
            throw new ArgumentException(
                "Destination address is required.",
                nameof(request.DestinationAddress));
        }

        var shipmentNumber = shipmentNumberGenerator.Generate();

        var shipment = ShipmentEntity.Create(
            tenantId: currentUser.TenantId.Value,
            shipmentNo: shipmentNumber,
            orderId: NormalizeOptionalText(request.OrderId),
            customerName: request.CustomerName.Trim(),
            destinationAddress: request.DestinationAddress.Trim());

        shipment.StatusHistories.Add(new ShipmentStatusHistory
        {
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.Created,
            Note = "Shipment created."
        });

        dbContext.Shipments.Add(shipment);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ShipmentDto.FromEntity(shipment);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
