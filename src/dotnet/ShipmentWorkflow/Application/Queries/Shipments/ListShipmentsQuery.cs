using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Queries.Shipments;

public sealed record ListShipmentsQuery(
    int Page,
    int Limit,
    string? Status,
    string? ShipmentNo,
    string? CustomerName,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo) : IRequest<ListShipmentsResult>;

public sealed class ListShipmentsQueryHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<ListShipmentsQuery, ListShipmentsResult>
{
    private const int MaxPageSize = 100;

    public async Task<ListShipmentsResult> Handle(
        ListShipmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
        {
            throw new DomainException("TenantId was not found in the authenticated user context.");
        }

        if (request.Page <= 0)
        {
            throw new DomainException("Page must be greater than zero.");
        }

        if (request.Limit <= 0 || request.Limit > MaxPageSize)
        {
            throw new DomainException($"Limit must be between 1 and {MaxPageSize}.");
        }

        var query = dbContext.Shipments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ShipmentStatus>(request.Status, ignoreCase: true, out var status))
            {
                throw new DomainException("Invalid shipment status filter.");
            }

            query = query.Where(shipment => shipment.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ShipmentNo))
        {
            var shipmentNo = request.ShipmentNo.Trim();
            query = query.Where(shipment => shipment.ShipmentNo.Contains(shipmentNo));
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            var customerName = request.CustomerName.Trim();
            query = query.Where(shipment => shipment.CustomerName.Contains(customerName));
        }

        if (request.CreatedFrom.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAt >= request.CreatedFrom.Value);
        }

        if (request.CreatedTo.HasValue)
        {
            query = query.Where(shipment => shipment.CreatedAt <= request.CreatedTo.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.Limit);

        var shipments = await query
            .OrderByDescending(shipment => shipment.CreatedAt)
            .ThenBy(shipment => shipment.Id)
            .Skip((request.Page - 1) * request.Limit)
            .Take(request.Limit)
            .Include(s => s.CargoItems)
            .Include(s => s.Locations)
            .Include(s => s.Documents)
            .Include(s => s.Milestones)
            .ToListAsync(cancellationToken);

        return new ListShipmentsResult(
            shipments.Select(ShipmentDto.FromEntity).ToArray(),
            request.Page,
            request.Limit,
            totalItems,
            totalPages);
    }
}
