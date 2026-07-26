using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Application.Queries.Routes;

public record ListPendingApprovalsQuery : IRequest<List<ApprovalRequestDto>>;

public class ListPendingApprovalsHandler(RoutePlanningDbContext context) 
    : IRequestHandler<ListPendingApprovalsQuery, List<ApprovalRequestDto>>
{
    public async Task<List<ApprovalRequestDto>> Handle(ListPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var approvals = await context.ApprovalRequests
            .Include(a => a.Route)
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return approvals.Select(a => new ApprovalRequestDto
        {
            Id = a.Id,
            RouteId = a.RouteId,
            RouteName = a.Route?.Name ?? string.Empty,
            Status = a.Status.ToString(),
            Reason = a.Reason,
            AiSummary = a.AiSummary,
            ComplianceSummary = a.ComplianceSummary,
            CreatedAt = a.CreatedAt
        }).ToList();
    }
}
