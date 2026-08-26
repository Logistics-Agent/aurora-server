using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MediatR;
using RoutePlanningAgent.Application.Commands.Configs;
using RoutePlanningAgent.Application.Commands.Policies;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Queries.Configs;
using RoutePlanningAgent.Application.Queries.Policies;
using RoutePlanningAgent.Application.Queries.Routes;
using RoutePlanningAgent.Grpc;
using Shared.Enums;

namespace RoutePlanningAgent.GrpcServices;

public class RoutePlanningGrpcService(IMediator mediator)
    : RoutePlanningService.RoutePlanningServiceBase
{
    // ===== Routes =====

    public override async Task<RouteResponse> CreateRoute(CreateRouteRequest request, ServerCallContext context)
    {
        var command = new CreateRouteCommand(
            request.Name,
            request.Description,
            request.RouteType,
            (decimal)request.MaxWeightKg,
            (decimal)request.MaxVolumeM3,
            (decimal)request.EstimatedDistanceKm,
            request.EstimatedDurationMinutes,
            MapStops(request.Stops)
        );

        var dto = await mediator.Send(command, context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    public override async Task<RouteResponse> GetRoute(GetRouteRequest request, ServerCallContext context)
    {
        var routeId = ParseGuid(request.Id, "Route ID");
        var dto = await mediator.Send(new GetRouteQuery(routeId), context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    public override async Task<ListRoutesResponse> ListRoutes(ListRoutesRequest request, ServerCallContext context)
    {
        var query = new ListRoutesQuery(request.Page, request.Limit, request.Status);
        var paged = await mediator.Send(query, context.CancellationToken);

        var response = new ListRoutesResponse
        {
            TotalItems = paged.TotalItems,
            Page = paged.Page,
            Limit = paged.Limit,
            TotalPages = paged.TotalPages
        };
        response.Routes.AddRange(paged.Items.Select(MapToRouteResponse));
        return response;
    }

    public override async Task<RouteResponse> UpdateRoute(UpdateRouteRequest request, ServerCallContext context)
    {
        var routeId = ParseGuid(request.Id, "Route ID");

        var command = new UpdateRouteCommand(
            routeId,
            request.Name,
            request.Description,
            request.RouteType,
            (decimal)request.MaxWeightKg,
            (decimal)request.MaxVolumeM3,
            (decimal)request.EstimatedDistanceKm,
            request.EstimatedDurationMinutes,
            MapStops(request.Stops)
        );

        var dto = await mediator.Send(command, context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    public override async Task<DeleteRouteResponse> DeleteRoute(DeleteRouteRequest request, ServerCallContext context)
    {
        var routeId = ParseGuid(request.Id, "Route ID");
        var success = await mediator.Send(new DeleteRouteCommand(routeId), context.CancellationToken);
        return new DeleteRouteResponse { Success = success };
    }

    public override async Task<RouteResponse> UpdateRouteStatus(UpdateRouteStatusRequest request, ServerCallContext context)
    {
        var routeId = ParseGuid(request.Id, "Route ID");
        var dto = await mediator.Send(
            new UpdateRouteStatusCommand(routeId, request.NewStatus), context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    public override async Task<RouteResponse> OptimizeRoute(OptimizeRouteRequest request, ServerCallContext context)
    {
        var routeId = ParseGuid(request.Id, "Route ID");
        var dto = await mediator.Send(new OptimizeRouteCommand(routeId), context.CancellationToken);
        return MapToRouteResponse(dto);
    }

    // ===== Approvals =====

    public override async Task<ApprovalResponse> ApproveRoute(ApproveRouteRequest request, ServerCallContext context)
    {
        var approvalId = ParseGuid(request.ApprovalId, "Approval ID");

        // is_approved = false không còn được hỗ trợ — reject phải dùng RejectRoute (bắt buộc reason)
        if (!request.IsApproved)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Dùng RejectRoute để từ chối (bắt buộc kèm reason). ApproveRoute chỉ dùng để phê duyệt."));

        var dto = await mediator.Send(
            new ApproveRouteCommand(approvalId, request.Comment), context.CancellationToken);
        return MapToApprovalResponse(dto);
    }

    public override async Task<ApprovalResponse> RejectRoute(RejectRouteRequest request, ServerCallContext context)
    {
        var approvalId = ParseGuid(request.ApprovalId, "Approval ID");

        var dto = await mediator.Send(
            new RejectRouteCommand(approvalId, request.Reason, request.Comment), context.CancellationToken);
        return MapToApprovalResponse(dto);
    }

    public override async Task<ListApprovalsResponse> ListPendingApprovals(ListPendingApprovalsRequest request, ServerCallContext context)
    {
        var paged = await mediator.Send(
            new ListPendingApprovalsQuery(request.Page, request.Limit), context.CancellationToken);

        var response = new ListApprovalsResponse
        {
            TotalItems = paged.TotalItems,
            Page = paged.Page,
            Limit = paged.Limit,
            TotalPages = paged.TotalPages
        };
        response.Approvals.AddRange(paged.Items.Select(MapToApprovalResponse));
        return response;
    }

    // ===== Recommendation =====

    public override async Task<RouteRecommendationResponse> GetRouteRecommendation(GetRouteRecommendationRequest request, ServerCallContext context)
    {
        var routeId = ParseGuid(request.RouteId, "Route ID");

        // Wire contract giữ nguyên tên RPC; bên trong là Command (ghi audit/approval/history)
        var dto = await mediator.Send(new RequestRouteRecommendationCommand(routeId), context.CancellationToken);

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

    // ===== Tenant AI / Rule configuration =====

    public override Task<TenantAiConfigResponse> GetTenantAiConfig(GetTenantAiConfigRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "AI configuration is centrally managed by AiGovernance service. RoutePlanningAgent does not own AI configuration."));
    }

    public override Task<TenantAiConfigResponse> UpsertTenantAiConfig(UpsertTenantAiConfigRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "AI configuration is centrally managed by AiGovernance service. RoutePlanningAgent does not own AI configuration."));
    }

    public override async Task<TenantRuleConfigResponse> UpsertTenantRuleConfig(UpsertTenantRuleConfigRequest request, ServerCallContext context)
    {
        var thresholds = request.Thresholds.ToDictionary(kv => kv.Key, kv => (decimal)kv.Value);
        var command = new UpsertTenantRuleConfigCommand(request.RuleName, request.IsEnabled, thresholds);
        var dto = await mediator.Send(command, context.CancellationToken);
        return MapToRuleConfigResponse(dto);
    }

    public override async Task<ListTenantRuleConfigsResponse> ListTenantRuleConfigs(ListTenantRuleConfigsRequest request, ServerCallContext context)
    {
        var paged = await mediator.Send(
            new ListTenantRuleConfigsQuery(request.Page, request.Limit), context.CancellationToken);

        var response = new ListTenantRuleConfigsResponse
        {
            TotalItems = paged.TotalItems,
            Page = paged.Page,
            Limit = paged.Limit
        };
        response.Configs.AddRange(paged.Items.Select(MapToRuleConfigResponse));
        return response;
    }

    // ===== Mapping helpers =====

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (!Guid.TryParse(value, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid {fieldName} format"));
        return id;
    }

    private static List<RouteStopInputDto> MapStops(IEnumerable<RouteStopInput> stops) =>
        stops.Select(s => new RouteStopInputDto
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

    private static ApprovalResponse MapToApprovalResponse(ApprovalRequestDto dto) => new()
    {
        Id = dto.Id.ToString(),
        RouteId = dto.RouteId.ToString(),
        RouteName = dto.RouteName,
        Status = dto.Status,
        Reason = dto.Reason,
        AiSummary = dto.AiSummary,
        ComplianceSummary = dto.ComplianceSummary ?? string.Empty,
        RejectionReason = dto.RejectionReason ?? string.Empty,
        CreatedAt = dto.CreatedAt.ToString("O")
    };

    // ===== Tenant Risk Policies =====

    public override async Task<RiskPolicyResponse> CreateRiskPolicyDraft(CreateRiskPolicyDraftRequest request, ServerCallContext context)
    {
        var source = Enum.TryParse<RiskPolicySource>(request.Source, true, out var parsedSource)
            ? parsedSource
            : RiskPolicySource.Tenant;

        var rules = request.Rules.Select(r => new TenantRiskRuleInputDto(
            r.RuleCode,
            r.RuleName,
            r.ThresholdsJson,
            r.RiskEffect,
            r.IsEnabled,
            r.SourceReference
        )).ToList();

        var command = new CreateTenantRiskPolicyDraftCommand(
            request.Name,
            request.Description,
            request.Scope,
            source,
            request.SourceDocumentId,
            rules
        );

        var dto = await mediator.Send(command, context.CancellationToken);
        return MapToPolicyResponse(dto);
    }

    public override async Task<RiskPolicyResponse> UpdateRiskPolicyDraft(UpdateRiskPolicyDraftRequest request, ServerCallContext context)
    {
        var policyId = ParseGuid(request.PolicyId, "Policy ID");

        var rules = request.Rules.Select(r => new TenantRiskRuleInputDto(
            r.RuleCode,
            r.RuleName,
            r.ThresholdsJson,
            r.RiskEffect,
            r.IsEnabled,
            r.SourceReference
        )).ToList();

        var command = new UpdateTenantRiskPolicyDraftCommand(
            policyId,
            request.Name,
            request.Description,
            rules
        );

        var dto = await mediator.Send(command, context.CancellationToken);
        return MapToPolicyResponse(dto);
    }

    public override async Task<RiskPolicyResponse> SubmitRiskPolicyForReview(SubmitRiskPolicyRequest request, ServerCallContext context)
    {
        var policyId = ParseGuid(request.PolicyId, "Policy ID");
        var dto = await mediator.Send(new SubmitTenantRiskPolicyCommand(policyId), context.CancellationToken);
        return MapToPolicyResponse(dto);
    }

    public override async Task<RiskPolicyResponse> RejectRiskPolicy(RejectRiskPolicyRequest request, ServerCallContext context)
    {
        var policyId = ParseGuid(request.PolicyId, "Policy ID");
        var dto = await mediator.Send(new RejectTenantRiskPolicyCommand(policyId, request.Reason, request.Comment), context.CancellationToken);
        return MapToPolicyResponse(dto);
    }

    public override async Task<RiskPolicyResponse> PublishRiskPolicy(PublishRiskPolicyRequest request, ServerCallContext context)
    {
        var policyId = ParseGuid(request.PolicyId, "Policy ID");
        var dto = await mediator.Send(new PublishTenantRiskPolicyCommand(policyId), context.CancellationToken);
        return MapToPolicyResponse(dto);
    }

    public override async Task<RiskPolicyResponse> GetRiskPolicy(GetRiskPolicyRequest request, ServerCallContext context)
    {
        var policyId = ParseGuid(request.PolicyId, "Policy ID");
        var dto = await mediator.Send(new GetTenantRiskPolicyByIdQuery(policyId), context.CancellationToken);
        return MapToPolicyResponse(dto);
    }

    public override async Task<RiskPolicyResponse> GetActiveRiskPolicy(GetActiveRiskPolicyRequest request, ServerCallContext context)
    {
        var dto = await mediator.Send(new GetActiveTenantRiskPolicyQuery(request.Scope), context.CancellationToken);
        if (dto == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Không tìm thấy chính sách rủi ro ACTIVE cho Scope '{request.Scope}'."));
        }
        return MapToPolicyResponse(dto);
    }

    public override async Task<ListRiskPolicyVersionsResponse> ListRiskPolicyVersions(ListRiskPolicyVersionsRequest request, ServerCallContext context)
    {
        var paged = await mediator.Send(new ListTenantRiskPolicyVersionsQuery(request.Scope, request.Page, request.Limit), context.CancellationToken);
        var response = new ListRiskPolicyVersionsResponse
        {
            TotalItems = paged.TotalItems,
            Page = paged.Page,
            Limit = paged.Limit
        };
        response.Policies.AddRange(paged.Items.Select(MapToPolicyResponse));
        return response;
    }

    public override async Task<DeleteRiskPolicyDraftResponse> DeleteRiskPolicyDraft(DeleteRiskPolicyDraftRequest request, ServerCallContext context)
    {
        var policyId = ParseGuid(request.PolicyId, "Policy ID");
        var success = await mediator.Send(new DeleteTenantRiskPolicyDraftCommand(policyId), context.CancellationToken);
        return new DeleteRiskPolicyDraftResponse { Success = success };
    }

    private static RiskPolicyResponse MapToPolicyResponse(TenantRiskPolicyDto dto)
    {
        var response = new RiskPolicyResponse
        {
            Id = dto.Id.ToString(),
            TenantId = dto.TenantId.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            Scope = dto.Scope,
            Version = dto.Version,
            Status = dto.Status,
            Source = dto.Source,
            SourceDocumentId = dto.SourceDocumentId ?? string.Empty,
            SubmittedByUserId = dto.SubmittedByUserId?.ToString() ?? string.Empty,
            SubmittedAt = dto.SubmittedAt?.ToString("O") ?? string.Empty,
            ReviewedByUserId = dto.ReviewedByUserId?.ToString() ?? string.Empty,
            ReviewedAt = dto.ReviewedAt?.ToString("O") ?? string.Empty,
            ReviewerComment = dto.ReviewerComment ?? string.Empty,
            PublishedByUserId = dto.PublishedByUserId?.ToString() ?? string.Empty,
            PublishedAt = dto.PublishedAt?.ToString("O") ?? string.Empty,
            RejectionReason = dto.RejectionReason ?? string.Empty,
            SupersededAt = dto.SupersededAt?.ToString("O") ?? string.Empty,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt?.ToString("O") ?? string.Empty
        };

        if (dto.Rules != null)
        {
            response.Rules.AddRange(dto.Rules.Select(r => new RiskRuleResponse
            {
                Id = r.Id.ToString(),
                PolicyId = r.PolicyId.ToString(),
                RuleCode = r.RuleCode,
                RuleName = r.RuleName,
                ThresholdsJson = r.ThresholdsJson,
                RiskEffect = r.RiskEffect,
                IsEnabled = r.IsEnabled,
                SourceReference = r.SourceReference ?? string.Empty,
                CreatedAt = r.CreatedAt.ToString("O")
            }));
        }

        return response;
    }

    private static TenantRuleConfigResponse MapToRuleConfigResponse(TenantRuleConfigDto dto)
    {
        var response = new TenantRuleConfigResponse
        {
            Id = dto.Id.ToString(),
            TenantId = dto.TenantId.ToString(),
            RuleName = dto.RuleName,
            IsEnabled = dto.IsEnabled,
            UpdatedAt = dto.UpdatedAt.ToString("O")
        };
        foreach (var (key, value) in dto.Thresholds)
        {
            response.Thresholds[key] = (double)value;
        }
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
