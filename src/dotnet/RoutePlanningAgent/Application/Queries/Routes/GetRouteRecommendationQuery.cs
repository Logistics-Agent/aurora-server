using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Rules;
using Shared.Enums;
using Shared.Events;
using Shared.Rules;
using Shared.Security;

namespace RoutePlanningAgent.Application.Queries.Routes;

public record GetRouteRecommendationQuery(Guid RouteId) : IRequest<RouteRecommendationDto>;

public class GetRouteRecommendationHandler(
    RoutePlanningDbContext context,
    IRouteRuleEngine ruleEngine,
    IRouteAiService aiService,
    IComplianceRagService complianceRag,
    ITenantAiConfigService configService,
    ITenantRuleConfigService ruleConfigService,
    IApprovalService approvalService,
    IPublishEndpoint publisher,
    ICurrentUserService currentUser)
    : IRequestHandler<GetRouteRecommendationQuery, RouteRecommendationDto>
{
    public async Task<RouteRecommendationDto> Handle(
        GetRouteRecommendationQuery request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? throw new Exception("Tenant ID context is missing");
        var userId = currentUser.UserId ?? throw new Exception("User ID context is missing");

        // 1. Load Route (Global query filters apply tenant isolation automatically)
        var route = await context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == request.RouteId, ct)
            ?? throw new Exception("Route not found");

        // 2. Read Automation Policy from TenantAiConfig (Cache-Aside)
        var config = await configService.GetConfigAsync(tenantId, "RoutePlanning", ct);
        var policy = config?.Policy ?? AutomationPolicy.RulesOnly;

        // 3. Manual Mode
        if (policy == AutomationPolicy.Manual)
        {
            await WriteAuditLogAsync(route.Id, tenantId, userId, policy,
                ruleResultsJson: "[]", decision: "ManualRequired", ct: ct);

            return new RouteRecommendationDto
            {
                RouteId = route.Id,
                RiskLevel = RouteRiskLevel.Low.ToString(),
                AutomationDecision = "ManualRequired",
                RecommendationSource = "None",
                Summary = "Automation policy is set to Manual. No automated analysis performed."
            };
        }

        // 4. Run Business Rule Engine
        var ruleContext = new RouteRuleContext(route, tenantId, ruleConfigService);
        var ruleResults = await ruleEngine.EvaluateAllAsync(ruleContext, ct);

        // Find max risk level
        var maxRisk = RouteRiskLevel.Low;
        if (ruleResults.Any())
        {
            maxRisk = ruleResults.Max(r => r.RiskLevel);
        }
        var needsComplianceCheck = ruleResults.Any(r => r.RequiresComplianceCheck);

        // 5. Rules Only Mode
        if (policy == AutomationPolicy.RulesOnly)
        {
            var decision = "ExecutedByRules";
            await WriteAuditLogAsync(route.Id, tenantId, userId, policy,
                ruleResultsJson: JsonSerializer.Serialize(ruleResults), decision: decision, ct: ct);

            return new RouteRecommendationDto
            {
                RouteId = route.Id,
                RiskLevel = maxRisk.ToString(),
                AutomationDecision = decision,
                RecommendationSource = "Rules",
                Summary = $"Rules evaluated successfully. Failed validations: {string.Join(", ", ruleResults.Where(r => !r.Passed).Select(r => r.Message))}"
            };
        }

        // 6. Call Compliance RAG check if required
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
                    tenantId, JsonSerializer.Serialize(route), riskReasons, ct);
            }
            catch (Exception ex)
            {
                // Soft fail RAG call, log error and continue with default compliance
                complianceResult = new ComplianceCheckResultDto
                {
                    HasViolations = true,
                    ViolationSummaries = [$"gRPC Compliance RAG service failed: {ex.Message}"],
                    MergedContext = "Fallback: Compliance check failed to contact service."
                };
            }
        }

        // 7. Call LLM with Rules & Compliance context
        var provider = config?.AiProvider ?? "Gemini";
        var aiResult = await aiService.GetRecommendationAsync(
            route, ruleResults, complianceResult, provider, ct);

        // 8. Determine if Human Approval is required
        bool needsApproval = policy == AutomationPolicy.RulesLlmApproval
            || ruleResults.Any(r => r.RequiresApproval)
            || (complianceResult?.RequiresHumanApproval ?? false);

        string finalDecision;
        Guid? approvalId = null;

        if (needsApproval)
        {
            var approval = await approvalService.CreateAsync(
                route.Id,
                reason: BuildApprovalReason(ruleResults, complianceResult),
                aiSummary: aiResult.Summary,
                complianceSummary: complianceResult?.MergedContext,
                ct);

            // Publish approval requested event
            await publisher.Publish(new RouteApprovalRequestedEvent
            {
                ApprovalRequestId = approval.Id,
                RouteId = route.Id,
                TenantId = tenantId,
                Reason = approval.Reason,
                AiSummary = aiResult.Summary
            }, ct);

            finalDecision = "PendingApproval";
            approvalId = approval.Id;
        }
        else
        {
            finalDecision = "ExecutedByLlm";
        }

        // 9. Write audit log
        await WriteAuditLogAsync(
            routeId: route.Id,
            tenantId: tenantId,
            userId: userId,
            policyApplied: policy,
            ruleResultsJson: JsonSerializer.Serialize(ruleResults),
            complianceCheckPerformed: needsComplianceCheck,
            complianceDocRefs: complianceResult != null ? string.Join(", ", complianceResult.DocumentRefs) : null,
            complianceSummary: complianceResult?.MergedContext,
            llmProvider: provider,
            llmModel: provider == "AzureOpenAI" ? "gpt-4o" : "gemini-2.5-flash",
            llmSummary: aiResult.Summary,
            decision: finalDecision,
            approvalRequestId: approvalId,
            ct: ct);

        return aiResult with
        {
            RiskLevel = maxRisk.ToString(),
            AutomationDecision = finalDecision,
            ApprovalRequestId = approvalId
        };
    }

    private string BuildApprovalReason(IReadOnlyList<RuleResult> ruleResults, ComplianceCheckResultDto? complianceResult)
    {
        var reasons = new List<string>();
        foreach (var rule in ruleResults.Where(r => r.RequiresApproval))
        {
            reasons.Add($"Violated {rule.RuleName}: {rule.Message}");
        }
        if (complianceResult != null && complianceResult.RequiresHumanApproval)
        {
            reasons.Add("Compliance RAG flagged hard constraint requiring human approval.");
        }
        return string.Join("; ", reasons);
    }

    private async Task WriteAuditLogAsync(
        Guid routeId,
        Guid tenantId,
        Guid userId,
        AutomationPolicy policyApplied,
        string ruleResultsJson,
        string decision,
        bool complianceCheckPerformed = false,
        string? complianceDocRefs = null,
        string? complianceSummary = null,
        string? llmProvider = null,
        string? llmModel = null,
        string? llmSummary = null,
        Guid? approvalRequestId = null,
        CancellationToken ct = default)
    {
        var audit = new RouteDecisionAuditLog
        {
            RouteId = routeId,
            TenantId = tenantId,
            RequestedByUserId = userId,
            PolicyApplied = policyApplied,
            RuleResultsJson = ruleResultsJson,
            RiskLevel = decision == "ManualRequired" ? RouteRiskLevel.Low.ToString() : "Evaluated",
            ComplianceCheckPerformed = complianceCheckPerformed,
            ComplianceDocumentRefs = complianceDocRefs,
            ComplianceSummary = complianceSummary,
            LlmProvider = llmProvider,
            LlmModel = llmModel,
            LlmSummary = llmSummary,
            AutomationDecision = decision,
            ApprovalRequestId = approvalRequestId
        };

        context.DecisionAuditLogs.Add(audit);
        await context.SaveChangesAsync(ct);
    }
}
