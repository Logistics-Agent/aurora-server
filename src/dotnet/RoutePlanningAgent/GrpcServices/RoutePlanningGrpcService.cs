using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MediatR;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Queries.Routes;
using RoutePlanningAgent.Grpc;

namespace RoutePlanningAgent.GrpcServices;

public class RoutePlanningGrpcService(IMediator mediator) 
    : RoutePlanningService.RoutePlanningServiceBase
{
    public override async Task<RouteResponse> CreateRoute(CreateRouteRequest request, ServerCallContext context)
    {
        var stops = request.Stops.Select(s => new RouteStopInputDto
        {
            Sequence = s.Sequence,
            StopType = s.StopType,
            LocationName = s.LocationName,
            Address = s.Address,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            EstimatedArrivalMinutes = s.EstimatedArrivalMinutes,
            ServiceDurationMinutes = s.ServiceDurationMinutes
        }).ToList();

        var command = new CreateRouteCommand(
            request.Name,
            request.Description,
            request.RouteType,
            (decimal)request.MaxWeightKg,
            (decimal)request.MaxVolumeM3,
            stops
        );

        var dto = await mediator.Send(command, context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    public override async Task<RouteResponse> GetRoute(GetRouteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var routeId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Route ID format"));

        var query = new GetRouteQuery(routeId);
        var dto = await mediator.Send(query, context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    public override async Task<ListRoutesResponse> ListRoutes(ListRoutesRequest request, ServerCallContext context)
    {
        var query = new ListRoutesQuery();
        var dtos = await mediator.Send(query, context.CancellationToken);

        var response = new ListRoutesResponse();
        response.Routes.AddRange(dtos.Select(MapToRouteResponse));
        return response;
    }

    public override async Task<ApprovalResponse> ApproveRoute(ApproveRouteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ApprovalId, out var approvalId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Approval ID format"));

        var command = new ApproveRouteCommand(approvalId, request.IsApproved, request.Comment);
        var dto = await mediator.Send(command, context.CancellationToken);

        return new ApprovalResponse
        {
            Id = dto.Id.ToString(),
            RouteId = dto.RouteId.ToString(),
            RouteName = dto.RouteName,
            Status = dto.Status,
            Reason = dto.Reason,
            AiSummary = dto.AiSummary,
            ComplianceSummary = dto.ComplianceSummary ?? string.Empty,
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    public override async Task<ListApprovalsResponse> ListPendingApprovals(ListPendingApprovalsRequest request, ServerCallContext context)
    {
        var query = new ListPendingApprovalsQuery();
        var dtos = await mediator.Send(query, context.CancellationToken);

        var response = new ListApprovalsResponse();
        response.Approvals.AddRange(dtos.Select(dto => new ApprovalResponse
        {
            Id = dto.Id.ToString(),
            RouteId = dto.RouteId.ToString(),
            RouteName = dto.RouteName,
            Status = dto.Status,
            Reason = dto.Reason,
            AiSummary = dto.AiSummary,
            ComplianceSummary = dto.ComplianceSummary ?? string.Empty,
            CreatedAt = dto.CreatedAt.ToString("O")
        }));

        return response;
    }

    public override async Task<RouteRecommendationResponse> GetRouteRecommendation(GetRouteRecommendationRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RouteId, out var routeId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Route ID format"));

        var query = new GetRouteRecommendationQuery(routeId);
        var dto = await mediator.Send(query, context.CancellationToken);

        var response = new RouteRecommendationResponse
        {
            RouteId = dto.RouteId.ToString(),
            RiskLevel = dto.RiskLevel,
            AutomationDecision = dto.AutomationDecision,
            RecommendationSource = dto.RecommendationSource,
            Summary = dto.Summary,
            ConfidenceScore = dto.ConfidenceScore ?? 0.0,
            ApprovalRequestId = dto.ApprovalRequestId?.ToString() ?? string.Empty
        };
        response.Suggestions.AddRange(dto.Suggestions);
        response.ApplicableRegulations.AddRange(dto.ApplicableRegulations);

        return response;
    }

    private static RouteResponse MapToRouteResponse(RouteDto dto)
    {
        var response = new RouteResponse
        {
            Id = dto.Id.ToString(),
            TenantId = dto.TenantId.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            RouteType = dto.RouteType,
            Status = dto.Status,
            RiskLevel = dto.RiskLevel,
            EstimatedDistanceKm = (double)dto.EstimatedDistanceKm,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            MaxWeightKg = (double)dto.MaxWeightKg,
            MaxVolumeM3 = (double)dto.MaxVolumeM3,
            IsAiGenerated = dto.IsAiGenerated,
            OptimizedAt = dto.OptimizedAt?.ToString("O") ?? string.Empty,
            Version = dto.Version,
            CreatedAt = dto.CreatedAt.ToString("O")
        };

        response.Stops.AddRange(dto.Stops.Select(s => new RouteStopResponse
        {
            Id = s.Id.ToString(),
            Sequence = s.Sequence,
            StopType = s.StopType,
            LocationName = s.LocationName,
            Address = s.Address,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            EstimatedArrivalMinutes = s.EstimatedArrivalMinutes,
            ServiceDurationMinutes = s.ServiceDurationMinutes
        }));

        return response;
    }
}
