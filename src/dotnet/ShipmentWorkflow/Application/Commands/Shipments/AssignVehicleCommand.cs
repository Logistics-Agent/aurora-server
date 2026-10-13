using System.Text.Json;
using MediatR;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record AssignVehicleCommand(
    Guid ShipmentId,
    string VehicleId,
    IReadOnlyList<Guid>? DocumentIds = null
) : IRequest<ShipmentDto>;

public sealed class AssignVehicleCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<AssignVehicleCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(AssignVehicleCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);

        if (string.IsNullOrWhiteSpace(request.VehicleId))
        {
            throw new DomainException("VehicleId (biển số xe) is required.");
        }

        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);

        var assignmentId = Guid.NewGuid().ToString("N");
        var normalizedPlate = request.VehicleId.Trim();

        shipment.AssignVehicle(normalizedPlate);

        // Link documents if provided
        if (request.DocumentIds is { Count: > 0 })
        {
            var requestedSet = request.DocumentIds.ToHashSet();
            var matchedDocs = shipment.Documents.Where(d => requestedSet.Contains(d.Id)).ToList();

            if (matchedDocs.Count != requestedSet.Count)
            {
                throw new DomainException("Một hoặc nhiều chứng từ không tồn tại hoặc không thuộc về chuyến hàng này.");
            }

            // Mark previous vehicle photos as not current
            foreach (var doc in shipment.Documents.Where(d => d.DocumentType == DocumentType.VehiclePhoto && !requestedSet.Contains(d.Id)))
            {
                if (!string.IsNullOrWhiteSpace(doc.ExtractedDataJson))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(doc.ExtractedDataJson);
                        if (dict != null)
                        {
                            dict["isCurrentVehicle"] = false;
                            doc.UpdateExtractedDataJson(JsonSerializer.Serialize(dict));
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors for legacy metadata
                    }
                }
            }

            // Tag matched docs with current vehicle assignment metadata
            foreach (var doc in matchedDocs)
            {
                var meta = new Dictionary<string, object>
                {
                    ["assignmentId"] = assignmentId,
                    ["licensePlate"] = normalizedPlate,
                    ["isCurrentVehicle"] = true,
                    ["assignedAt"] = DateTimeOffset.UtcNow.ToString("O")
                };
                doc.UpdateExtractedDataJson(JsonSerializer.Serialize(meta));
            }
        }

        ShipmentCommandHelpers.AddShipmentUpdatedOutbox(
            dbContext,
            shipment,
            [nameof(shipment.VehicleId)]);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
