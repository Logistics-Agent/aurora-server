using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Rules;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Rules;
using Shared.Security;
using Route = RoutePlanningAgent.Domain.Route;

namespace RoutePlanningAgent.Application.Commands.Routes;

/// <summary>
/// Đánh giá route theo mô hình quản trị rủi ro vận hành (Risk-based Operational Governance):
/// resolve effective policy → rule engine → compliance RAG → AiGovernance (route.plan) → governance decision engine.
/// Toàn bộ cấu hình AI, quota, provider/model thuộc quyền quản lý của AiGovernance.
/// RoutePlanningAgent sở hữu chính sách rủi ro vận hành (TenantRiskPolicyConfig) và quyết định thẩm quyền.
/// Manager KHÔNG còn là bottleneck cho các tác vụ LOW/MEDIUM thông thường.
/// Toàn bộ ghi trong MỘT SaveChangesAsync — atomic cùng outbox events.
/// </summary>
public record RequestRouteRecommendationCommand(Guid RouteId) : IRequest<RouteRecommendationDto>;

public class RequestRouteRecommendationHandler(
    RoutePlanningDbContext context,
    IRouteRuleEngine ruleEngine,
    IRouteAiService aiService,
    IComplianceRagService complianceRag,
    ITenantRuleConfigService ruleConfigService,
    IRouteRiskPolicyProvider policyProvider,
    IApprovalService approvalService,
    IRouteGovernanceService governanceService,
    IOutboxWriter outbox,
    ICurrentUserService currentUser,
    ILogger<RequestRouteRecommendationHandler> logger)
    : IRequestHandler<RequestRouteRecommendationCommand, RouteRecommendationDto>
{
    private const string Feature = "RoutePlanning";

    public async Task<RouteRecommendationDto> Handle(
        RequestRouteRecommendationCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        // 1. Load Route (global query filter tự áp tenant isolation)
        var route = await context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == request.RouteId, ct)
            ?? throw new NotFoundException($"Route '{request.RouteId}' not found");

        // 2. Phân giải Effective Risk Policy cho Tenant (Tenant Custom > Platform Default, Fail-Closed nếu chưa cấu hình)
        var effectivePolicy = await policyProvider.GetEffectivePolicyAsync(tenantId, Feature, ct);

        // 3. Chạy Deterministic Rule Engine
        var ruleContext = new RouteRuleContext(route, tenantId, ruleConfigService);
        var ruleResults = await ruleEngine.EvaluateAllAsync(ruleContext, ct);

        // 4. Compliance RAG (nếu rule yêu cầu kiểm tra tuân thủ pháp lý)
        var needsComplianceCheck = ruleResults.Any(r => r.RequiresComplianceCheck);
        var routeDto = RouteMapper.ToDto(route);
        ComplianceCheckResultDto? complianceResult = null;

        if (needsComplianceCheck)
        {
            var riskReasons = ruleResults
                .Where(r => !r.Passed)
                .Select(r => r.Message ?? r.RuleName)
                .ToList();

            try
            {
                complianceResult = await complianceRag.CheckComplianceAsync(
                    tenantId, JsonSerializer.Serialize(routeDto), riskReasons, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Compliance RAG call failed for route {RouteId} — dùng fallback", route.Id);
                complianceResult = new ComplianceCheckResultDto
                {
                    HasViolations = true,
                    ViolationSummaries = [$"Compliance RAG service không khả dụng: {ex.Message}"],
                    MergedContext = "Fallback: không liên lạc được Compliance RAG service."
                };
            }
        }

        // 5. Gọi AI qua AiGovernance trung tâm (capability: route.plan)
        var aiResult = await aiService.GetRecommendationAsync(
            routeDto, ruleResults, complianceResult, ct);

        // 6. Đánh giá rủi ro tổng hợp qua Governance Service
        var compositeGovernance = await governanceService.AssessRouteAsync(
            route, effectivePolicy, ruleResults, complianceResult, aiResult, ct);

        return await ProcessGovernanceResultAsync(
            route, tenantId, userId, effectivePolicy, compositeGovernance,
            ruleResults, complianceResult, aiResult, ct);
    }

    private async Task<RouteRecommendationDto> ProcessGovernanceResultAsync(
        Route route,
        Guid tenantId,
        Guid userId,
        EffectiveRiskPolicy effectivePolicy,
        RiskAssessmentResult governance,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult,
        RouteAiResult? aiResult,
        CancellationToken ct)
    {
        route.RiskLevel = governance.RiskLevel;
        route.GovernanceDecision = governance.Decision;
        route.LastAssessedAt = DateTimeOffset.UtcNow;

        Guid? approvalId = null;
        string finalDecision;

        if (governance.Decision == GovernanceDecision.ManagerApprovalRequired)
        {
            // Rủi ro cao hoặc vi phạm bắt buộc: Tạo ApprovalRequest gắn chặt với RouteVersion và PolicyVersion
            var reason = BuildApprovalReason(ruleResults, complianceResult, governance.ReasonDetails);
            var aiSummary = aiResult?.Recommendation.Summary ?? governance.ReasonDetails;
            var compSummary = complianceResult?.MergedContext;

            var approval = await approvalService.CreateAsync(
                route.Id,
                reason: reason,
                aiSummary: aiSummary,
                complianceSummary: compSummary,
                routeVersion: route.Version,
                policyId: effectivePolicy.PolicyId,
                policyVersion: effectivePolicy.Version,
                ct: ct);

            approvalId = approval.Id;
            finalDecision = "PendingApproval";

            outbox.Enqueue(new RouteApprovalRequestedEvent
            {
                ApprovalRequestId = approval.Id,
                RouteId = route.Id,
                TenantId = tenantId,
                Reason = approval.Reason,
                AiSummary = aiSummary
            });
        }
        else if (governance.Decision == GovernanceDecision.Blocked)
        {
            finalDecision = "Blocked";
        }
        else
        {
            // LOW (NoApprovalRequired) hoặc MEDIUM (StaffAllowed): Nhân viên được phép thực thi -> Route tự động sẵn sàng (Ready)
            if (route.Status is RouteStatus.Draft or RouteStatus.Optimizing)
            {
                route.Status = RouteStatus.Ready;
            }

            finalDecision = (aiResult != null && aiResult.Success) ? "ExecutedByAi" : "ExecutedByRules";
        }

        // Lưu bản ghi RiskAssessment bất biến phục vụ kiểm toán và phát hiện stale
        AddRiskAssessment(
            route.Id, route.Version, tenantId, userId,
            governance.RiskLevel, governance.Decision, governance.Source,
            effectivePolicy.PolicyId, effectivePolicy.Version, effectivePolicy.Source,
            governance.MatchedRuleCodes,
            governance.ReasonCodes, governance.ReasonDetails,
            policy: effectivePolicy.PolicyId, confidence: governance.ConfidenceScore);

        // Lưu AI Optimization History nếu có gọi LLM thành công
        if (aiResult is not null && aiResult.Success)
        {
            context.OptimizationHistories.Add(new RouteOptimizationHistory
            {
                RouteId = route.Id,
                Provider = aiResult.Provider,
                Model = aiResult.Model,
                PromptVersion = aiResult.PromptVersion,
                TotalDistanceKm = route.EstimatedDistanceKm,
                TotalDurationMinutes = route.EstimatedDurationMinutes,
                InputTokens = aiResult.InputTokens,
                OutputTokens = aiResult.OutputTokens
            });

            outbox.Enqueue(new AiUsageEvent
            {
                TenantId = tenantId,
                ServiceName = "RoutePlanningAgent",
                Feature = "RouteRecommendation",
                Provider = aiResult.Provider,
                Model = aiResult.Model,
                PromptVersion = aiResult.PromptVersion,
                InputTokens = aiResult.InputTokens,
                OutputTokens = aiResult.OutputTokens,
                LatencyMs = aiResult.LatencyMs,
                Success = aiResult.Success,
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        // Audit Log
        AddAuditLog(route.Id, tenantId, userId, effectivePolicy.PolicyId,
            ruleResultsJson: JsonSerializer.Serialize(ruleResults),
            riskLevel: governance.RiskLevel.ToString(),
            decision: finalDecision,
            complianceCheckPerformed: complianceResult != null,
            complianceDocRefs: complianceResult != null ? string.Join(", ", complianceResult.DocumentRefs) : null,
            complianceSummary: complianceResult?.MergedContext,
            llmProvider: aiResult?.Provider,
            llmModel: aiResult?.Model,
            llmSummary: aiResult?.Recommendation.Summary,
            approvalRequestId: approvalId);

        // Outbox: Phát sự kiện đánh giá rủi ro thành công kèm đầy đủ dữ liệu provenance
        outbox.Enqueue(new RouteRiskEvaluatedEvent
        {
            RouteId = route.Id,
            RouteVersion = route.Version,
            TenantId = tenantId,
            RiskLevel = governance.RiskLevel.ToString(),
            GovernanceDecision = governance.Decision.ToString(),
            PolicyId = effectivePolicy.PolicyId,
            PolicyVersion = effectivePolicy.Version,
            PolicySource = effectivePolicy.Source.ToString(),
            MatchedRuleCodes = governance.MatchedRuleCodes.ToArray(),
            Source = governance.Source,
            EvaluatedByUserId = userId
        });

        await context.SaveChangesAsync(ct);

        if (aiResult is not null && aiResult.Success)
        {
            return aiResult.Recommendation with
            {
                RiskLevel = governance.RiskLevel.ToString(),
                AutomationDecision = finalDecision,
                ApprovalRequestId = approvalId
            };
        }

        var failedMessages = ruleResults.Where(r => !r.Passed).Select(r => r.Message);
        return new RouteRecommendationDto
        {
            RouteId = route.Id,
            RiskLevel = governance.RiskLevel.ToString(),
            AutomationDecision = finalDecision,
            RecommendationSource = "Rules",
            ApprovalRequestId = approvalId,
            Summary = failedMessages.Any()
                ? $"Rule engine đã đánh giá ({governance.RiskLevel}) theo chính sách {effectivePolicy.PolicyId} v{effectivePolicy.Version}. Vi phạm: {string.Join("; ", failedMessages)}"
                : $"Đánh giá rủi ro thành công ({governance.RiskLevel}) theo chính sách {effectivePolicy.PolicyId} v{effectivePolicy.Version}. Quyết định: {governance.Decision}."
        };
    }

    private static string BuildApprovalReason(
        IReadOnlyList<RuleResult> ruleResults, ComplianceCheckResultDto? complianceResult, string fallback)
    {
        var reasons = new List<string>();
        foreach (var rule in ruleResults.Where(r => r.RequiresApproval || !r.Passed))
        {
            reasons.Add($"Vi phạm {rule.RuleName}: {rule.Message}");
        }
        if (complianceResult is { RequiresHumanApproval: true })
        {
            reasons.Add("Compliance RAG đánh dấu ràng buộc cứng — cần người phê duyệt.");
        }
        return reasons.Count > 0 ? string.Join("; ", reasons) : fallback;
    }

    private void AddRiskAssessment(
        Guid routeId,
        int routeVersion,
        Guid tenantId,
        Guid userId,
        RouteRiskLevel riskLevel,
        GovernanceDecision decision,
        string source,
        string policyId,
        int policyVersion,
        RiskPolicySource policySource,
        IReadOnlyList<string> matchedRuleCodes,
        IReadOnlyList<string> reasonCodes,
        string reasonDetails,
        string policy,
        double? confidence = null)
    {
        context.RiskAssessments.Add(new RiskAssessment
        {
            RouteId = routeId,
            RouteVersion = routeVersion,
            TenantId = tenantId,
            RiskLevel = riskLevel,
            GovernanceDecision = decision,
            Source = source,
            PolicyId = policyId,
            PolicyVersion = policyVersion,
            PolicySource = policySource,
            MatchedRuleCodes = JsonSerializer.Serialize(matchedRuleCodes),
            ReasonCodes = JsonSerializer.Serialize(reasonCodes),
            ReasonDetails = reasonDetails,
            PolicyApplied = policy,
            ConfidenceScore = confidence,
            AssessedByUserId = userId,
            AssessedAt = DateTimeOffset.UtcNow
        });
    }

    private void AddAuditLog(
        Guid routeId,
        Guid tenantId,
        Guid userId,
        string policyApplied,
        string ruleResultsJson,
        string riskLevel,
        string decision,
        bool complianceCheckPerformed = false,
        string? complianceDocRefs = null,
        string? complianceSummary = null,
        string? llmProvider = null,
        string? llmModel = null,
        string? llmSummary = null,
        Guid? approvalRequestId = null)
    {
        context.DecisionAuditLogs.Add(new RouteDecisionAuditLog
        {
            RouteId = routeId,
            TenantId = tenantId,
            RequestedByUserId = userId,
            PolicyApplied = policyApplied,
            RuleResultsJson = ruleResultsJson,
            RiskLevel = riskLevel,
            ComplianceCheckPerformed = complianceCheckPerformed,
            ComplianceDocumentRefs = complianceDocRefs,
            ComplianceSummary = complianceSummary,
            LlmProvider = llmProvider,
            LlmModel = llmModel,
            LlmSummary = llmSummary,
            AutomationDecision = decision,
            ApprovalRequestId = approvalRequestId
        });
    }
}
