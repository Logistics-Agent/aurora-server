using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using Shipment.Contracts.Events;
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
    string DestinationAddress,
    IReadOnlyCollection<CreateShipmentCargoItem> CargoItems
) : IRequest<ShipmentDto>;

public sealed record CreateShipmentCargoItem(
    string Name,
    int Quantity,
    double WeightKg,
    string? HsCode);

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
            throw new DomainException(
                "TenantId was not found in the authenticated user context.");
        }

        ValidateRequest(request);

        var tenantId = currentUser.TenantId.Value;
        var shipmentNumber = await GenerateUniqueShipmentNumberAsync(
            tenantId,
            cancellationToken);

        var shipment = ShipmentEntity.Create(
            tenantId: tenantId,
            shipmentNo: shipmentNumber,
            orderId: NormalizeOptionalText(request.OrderId),
            customerName: request.CustomerName,
            destinationAddress: request.DestinationAddress);

        foreach (var cargoItem in request.CargoItems)
        {
            shipment.AddCargoItem(
                cargoItem.Name,
                cargoItem.Quantity,
                cargoItem.WeightKg,
                cargoItem.HsCode);
        }

        shipment.StatusHistories.Add(new ShipmentStatusHistory
        {
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.Created,
            Note = "Shipment created."
        });

        var shipmentCreatedEvent = new ShipmentCreatedEvent
        {
            ShipmentId = shipment.Id,
            TenantId = shipment.TenantId,
            ShipmentNumber = shipment.ShipmentNo,
            OrderId = shipment.OrderId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            EventType = nameof(ShipmentCreatedEvent),
            Payload = JsonSerializer.Serialize(shipmentCreatedEvent),
            CreatedAt = shipmentCreatedEvent.CreatedAt
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        dbContext.Shipments.Add(shipment);
        dbContext.OutboxMessages.Add(outboxMessage);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ShipmentDto.FromEntity(shipment);
    }

    private async Task<string> GenerateUniqueShipmentNumberAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var shipmentNumber = shipmentNumberGenerator.Generate();
            var exists = await dbContext.Shipments.AnyAsync(
                shipment =>
                    shipment.TenantId == tenantId &&
                    shipment.ShipmentNo == shipmentNumber,
                cancellationToken);

            if (!exists)
            {
                return shipmentNumber;
            }
        }

        throw new ConflictException(
            "Could not generate a unique shipment number.");
    }

    private static void ValidateRequest(CreateShipmentCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new DomainException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DestinationAddress))
        {
            throw new DomainException("Destination address is required.");
        }

        foreach (var cargoItem in request.CargoItems)
        {
            if (string.IsNullOrWhiteSpace(cargoItem.Name))
            {
                throw new DomainException("Cargo item name is required.");
            }

            if (cargoItem.Quantity <= 0)
            {
                throw new DomainException(
                    "Cargo quantity must be greater than zero.");
            }

            if (cargoItem.WeightKg < 0)
            {
                throw new DomainException(
                    "Cargo weight must not be negative.");
            }
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
