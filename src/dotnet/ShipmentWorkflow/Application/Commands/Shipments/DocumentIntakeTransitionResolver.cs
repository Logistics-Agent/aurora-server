using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

internal static class DocumentIntakeTransitionResolver
{
    internal static async Task<DocumentIntakeDto> ResolveAsync(
        ShipmentWorkflowDbContext dbContext,
        Guid intakeId,
        DocumentIntakeStatus expectedStatus,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var current = await dbContext.DocumentIntakes.SingleOrDefaultAsync(
            intake => intake.Id == intakeId,
            cancellationToken) ?? throw new NotFoundException("Document intake was not found.");
        if (current.Status == expectedStatus)
            return DocumentIntakeDto.FromEntity(current);

        throw new ConflictException("The document intake was changed concurrently.");
    }
}
