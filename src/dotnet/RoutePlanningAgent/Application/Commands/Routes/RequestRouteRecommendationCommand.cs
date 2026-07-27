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
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Rules;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Rules;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

/// <summary>
/// Đánh giá route theo automation policy của tenant: rule engine → compliance → LLM → approval.
/// Là COMMAND (không phải Query) vì nó ghi: audit log, approval request, optimization history, outbox.
/// Toàn bộ ghi trong MỘT SaveChangesAsync — atomic cùng outbox events.
/// gRPC method GetRouteRecommendation map vào command này (wire contract không đổi).
/// </summary>
public record RequestRouteRecommendationCommand(Guid RouteId) : IRequest<RouteRecommendationDto>;

public class RequestRouteRecommendationHandler(
    RoutePlanningDbContext context,
    IRouteRuleEngine ruleEngine,
    IRouteAiService aiService,
    IComplianceRagService complianceRag,
    ITenantAiConfigService configService,
    ITenantRuleConfigService ruleConfigService,
    IApprovalService approvalService,
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

        // 2. Đọc Automation Policy từ TenantAiConfig (cache-aside)
        var config = await configService.GetConfigAsync(tenantId, Feature, ct);
        var policy = config?.Policy ?? AutomationPolicy.RulesOnly;

        // 3. Manual mode — không phân tích tự động
        if (policy == AutomationPolicy.Manual)
        {
            AddAuditLog(route.Id, tenantId, userId, policy,
                ruleResultsJson: "[]",
                riskLevel: RouteRiskLevel.Low.ToString(),
                decision: "ManualRequired");

            await context.SaveChangesAsync(ct);

            return new RouteRecommendationDto
            {
                RouteId = route.Id,
                RiskLevel = RouteRiskLevel.Low.ToString(),
                AutomationDecision = "ManualRequired",
                RecommendationSource = "None",
                Summary = "Automation policy đang là Manual — không thực hiện phân tích tự động."
            };
        }

        // 4. Chạy rule engine
        var ruleContext = new RouteRuleContext(route, tenantId, ruleConfigService);
        var ruleResults = await ruleEngine.EvaluateAllAsync(ruleContext, ct);

        var maxRisk = ruleResults.Count > 0 ? ruleResults.Max(r => r.RiskLevel) : RouteRiskLevel.Low;
        var needsComplianceCheck = ruleResults.Any(r => r.RequiresComplianceCheck);

        // Ghi nhận risk đã đánh giá lên chính route
        route.RiskLevel = maxRisk;

        // 5. RulesOnly mode
        if (policy == AutomationPolicy.RulesOnly)
        {
            const string decision = "ExecutedByRules";
            AddAuditLog(route.Id, tenantId, userId, policy,
                ruleResultsJson: JsonSerializer.Serialize(ruleResults),
                riskLevel: maxRisk.ToString(),
                decision: decision);

            await context.SaveChangesAsync(ct);

            var failedMessages = ruleResults.Where(r => !r.Passed).Select(r => r.Message);
            return new RouteRecommendationDto
            {
                RouteId = route.Id,
                RiskLevel = maxRisk.ToString(),
                AutomationDecision = decision,
                RecommendationSource = "Rules",
                Summary = $"Rule engine đã đánh giá. Vi phạm: {string.Join("; ", failedMessages)}"
            };
        }

        // 6. Compliance RAG (nếu rule yêu cầu) — soft-fail, service chưa triển khai
        var routeDto = RouteMapper.ToDto(route); // serialize DTO — không cycle
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

        // 7. Gọi LLM (Gemini/AzureOpenAI) với ngữ cảnh rules + compliance
        var provider = config?.AiProvider ?? "Gemini";
        var aiResult = await aiService.GetRecommendationAsync(
            routeDto, ruleResults, complianceResult, provider, ct);

        // 8. Xác định có cần người duyệt không
        var needsApproval = policy == AutomationPolicy.RulesLlmApproval
            || ruleResults.Any(r => r.RequiresApproval)
            || (complianceResult?.RequiresHumanApproval ?? false);

        string finalDecision;
        Guid? approvalId = null;

        if (needsApproval)
        {
            // CreateAsync KHÔNG SaveChanges — nằm chung transaction với audit + history + outbox
            var approval = await approvalService.CreateAsync(
                route.Id,
                reason: BuildApprovalReason(ruleResults, complianceResult),
                aiSummary: aiResult.Recommendation.Summary,
                complianceSummary: complianceResult?.MergedContext,
                ct);

            outbox.Enqueue(new RouteApprovalRequestedEvent
            {
                ApprovalRequestId = approval.Id,
                RouteId = route.Id,
                TenantId = tenantId,
                Reason = approval.Reason,
                AiSummary = aiResult.Recommendation.Summary
            });

            finalDecision = "PendingApproval";
            approvalId = approval.Id;
        }
        else
        {
            finalDecision = "ExecutedByLlm";
        }

        // 9. Optimization history + AI usage event — token usage THẬT từ LLM response
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

        // 10. Audit log — RiskLevel là giá trị TÍNH được (không phải literal)
        AddAuditLog(route.Id, tenantId, userId, policy,
            ruleResultsJson: JsonSerializer.Serialize(ruleResults),
            riskLevel: maxRisk.ToString(),
            decision: finalDecision,
            complianceCheckPerformed: needsComplianceCheck,
            complianceDocRefs: complianceResult != null ? string.Join(", ", complianceResult.DocumentRefs) : null,
            complianceSummary: complianceResult?.MergedContext,
            llmProvider: aiResult.Provider,
            llmModel: aiResult.Model,
            llmSummary: aiResult.Recommendation.Summary,
            approvalRequestId: approvalId);

        // MỘT transaction duy nhất: route.RiskLevel + approval + history + audit + outbox
        await context.SaveChangesAsync(ct);

        return aiResult.Recommendation with
        {
            RiskLevel = maxRisk.ToString(),
            AutomationDecision = finalDecision,
            ApprovalRequestId = approvalId
        };
    }

    private static string BuildApprovalReason(
        IReadOnlyList<RuleResult> ruleResults, ComplianceCheckResultDto? complianceResult)
    {
        var reasons = new List<string>();
        foreach (var rule in ruleResults.Where(r => r.RequiresApproval))
        {
            reasons.Add($"Vi phạm {rule.RuleName}: {rule.Message}");
        }
        if (complianceResult is { RequiresHumanApproval: true })
        {
            reasons.Add("Compliance RAG đánh dấu ràng buộc cứng — cần người phê duyệt.");
        }
        return reasons.Count > 0 ? string.Join("; ", reasons) : "Chính sách yêu cầu người phê duyệt.";
    }

    private void AddAuditLog(
        Guid routeId,
        Guid tenantId,
        Guid userId,
        AutomationPolicy policyApplied,
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
        // Không SaveChanges — handler chính gọi một lần
    }
}
