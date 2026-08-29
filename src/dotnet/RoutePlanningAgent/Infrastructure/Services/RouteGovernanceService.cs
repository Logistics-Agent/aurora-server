using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;
using Shared.Exceptions;
using Shared.Rules;
using Route = RoutePlanningAgent.Domain.Route;

namespace RoutePlanningAgent.Infrastructure.Services;

public class RouteGovernanceService(RoutePlanningDbContext context) : IRouteGovernanceService
{
    public Task<RiskAssessmentResult> AssessRouteAsync(
        Route route,
        EffectiveRiskPolicy effectivePolicy,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult = null,
        RouteAiResult? aiResult = null,
        CancellationToken ct = default)
    {
        var matchedRuleCodes = new List<string>();
        var reasonCodes = new List<string>();
        var reasonDetailsList = new List<string>();

        // 1. Tính toán rủi ro từ Rule Engine (Deterministic Rules)
        var deterministicRisk = RouteRiskLevel.Low;
        var ruleRequiresApproval = false;

        foreach (var rule in ruleResults)
        {
            if (!rule.Passed)
            {
                var code = !string.IsNullOrWhiteSpace(rule.RuleCode) ? rule.RuleCode : rule.RuleName;
                matchedRuleCodes.Add(code);
                reasonCodes.Add(rule.RuleName);
                if (!string.IsNullOrWhiteSpace(rule.Message))
                    reasonDetailsList.Add($"{code}: {rule.Message}");
            }

            if (rule.RiskLevel > deterministicRisk)
            {
                deterministicRisk = rule.RiskLevel;
            }

            if (rule.RequiresApproval)
            {
                ruleRequiresApproval = true;
            }
        }

        // 2. Tính toán tín hiệu từ Compliance RAG
        var complianceRequiresApproval = false;
        if (complianceResult is not null)
        {
            if (complianceResult.HasViolations)
            {
                matchedRuleCodes.Add("REGULATORY_COMPLIANCE_VIOLATION");
                reasonCodes.Add("ComplianceViolations");
                reasonDetailsList.AddRange(complianceResult.ViolationSummaries);

                if (deterministicRisk < RouteRiskLevel.High)
                {
                    deterministicRisk = RouteRiskLevel.High;
                }
            }

            if (complianceResult.RequiresHumanApproval)
            {
                complianceRequiresApproval = true;
                if (deterministicRisk < RouteRiskLevel.High)
                {
                    deterministicRisk = RouteRiskLevel.High;
                }
            }
        }

        // 3. Tính toán tín hiệu từ AI (LLM) — AI chỉ là MỘT tín hiệu, không ghi đè quy tắc tất định
        var aiRisk = RouteRiskLevel.Low;
        double? confidenceScore = null;
        if (aiResult is not null)
        {
            confidenceScore = aiResult.Recommendation.ConfidenceScore;
            if (Enum.TryParse<RouteRiskLevel>(aiResult.Recommendation.RiskLevel, true, out var parsedAiRisk))
            {
                aiRisk = parsedAiRisk;
            }
        }

        // 4. Tổng hợp mức độ rủi ro hiệu lực (Effective Risk)
        // QUY TẮC BẢO VỆ: AI không bao giờ được phép hạ rủi ro xuống dưới mức quy tắc tất định đã xác định
        var effectiveRisk = (RouteRiskLevel)Math.Max((int)deterministicRisk, (int)aiRisk);

        // 5. Xác định quyết định quản trị (Governance Decision)
        // QUY TẮC BẢO VỆ: Ngay cả khi AI được cấp phép FULL_AUTONOMOUS, rủi ro phân loại trong RoutePlanning vẫn quyết định phê duyệt
        GovernanceDecision decision;
        if (ruleRequiresApproval || complianceRequiresApproval)
        {
            decision = GovernanceDecision.ManagerApprovalRequired;
        }
        else
        {
            decision = effectiveRisk switch
            {
                RouteRiskLevel.Low => GovernanceDecision.NoApprovalRequired,
                RouteRiskLevel.Medium => GovernanceDecision.StaffAllowed,
                RouteRiskLevel.High => GovernanceDecision.ManagerApprovalRequired,
                RouteRiskLevel.Critical => GovernanceDecision.Blocked,
                _ => GovernanceDecision.StaffAllowed
            };
        }

        var source = aiResult != null ? "Composite" : (complianceResult != null ? "RulesAndCompliance" : "DeterministicRules");
        var details = reasonDetailsList.Count > 0
            ? string.Join("; ", reasonDetailsList)
            : $"Đánh giá rủi ro {effectiveRisk} theo chính sách {effectivePolicy.PolicyId} v{effectivePolicy.Version} — Quyết định: {decision}";

        var result = new RiskAssessmentResult
        {
            RiskLevel = effectiveRisk,
            Decision = decision,
            Source = source,
            PolicyId = effectivePolicy.PolicyId,
            PolicyVersion = effectivePolicy.Version,
            PolicySource = effectivePolicy.Source,
            MatchedRuleCodes = matchedRuleCodes,
            ReasonCodes = reasonCodes,
            ReasonDetails = details,
            ConfidenceScore = confidenceScore,
            RuleResults = ruleResults
        };

        return Task.FromResult(result);
    }

    public RiskAssessmentResult AssessSoftDeleteRisk(
        Route route,
        EffectiveRiskPolicy effectivePolicy)
    {
        var matchedRuleCodes = new List<string> { "ROUTE_SOFT_DELETE_OPERATION" };
        var reasonCodes = new List<string> { "RouteSoftDelete" };

        var (riskLevel, decision, details) = route.Status switch
        {
            RouteStatus.Active => (
                RouteRiskLevel.Critical,
                GovernanceDecision.Blocked,
                "Không thể xóa tuyến đường đang ở trạng thái 'Active' (đang chạy thực tế). Hãy chuyển trạng thái hoặc hủy trước."
            ),
            RouteStatus.Ready => (
                RouteRiskLevel.Medium,
                GovernanceDecision.StaffAllowed,
                "Xóa tuyến đường đã ở trạng thái 'Ready' (sẵn sàng vận hành)."
            ),
            RouteStatus.Completed or RouteStatus.Cancelled or RouteStatus.Archived => (
                RouteRiskLevel.Low,
                GovernanceDecision.StaffAllowed,
                $"Xóa tuyến đường đã kết thúc hoặc lưu trữ (trạng thái '{route.Status}')."
            ),
            _ => (
                RouteRiskLevel.Low,
                GovernanceDecision.StaffAllowed,
                "Xóa tuyến đường bản nháp (Draft/Optimizing)."
            )
        };

        return new RiskAssessmentResult
        {
            RiskLevel = riskLevel,
            Decision = decision,
            Source = "SoftDeleteGovernance",
            PolicyId = effectivePolicy.PolicyId,
            PolicyVersion = effectivePolicy.Version,
            PolicySource = effectivePolicy.Source,
            MatchedRuleCodes = matchedRuleCodes,
            ReasonCodes = reasonCodes,
            ReasonDetails = details
        };
    }

    public async Task ValidateExecutionAuthorizedAsync(
        Route route,
        EffectiveRiskPolicy effectivePolicy,
        CancellationToken ct = default)
    {
        // 1. Kiểm tra bản đánh giá rủi ro gần nhất (Load và sort client-side để tương thích đa CSDL SQLite/PostgreSQL)
        var assessments = await context.RiskAssessments
            .AsNoTracking()
            .Where(a => a.RouteId == route.Id)
            .ToListAsync(ct);

        var latestAssessment = assessments
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (latestAssessment is null)
        {
            throw new ForbiddenException(
                $"Tuyến đường '{route.Id}' chưa được đánh giá rủi ro vận hành. " +
                $"Bắt buộc chạy GetRouteRecommendation để đánh giá rủi ro trước khi thực thi.");
        }

        // 2. Bảo vệ Route Version: Đánh giá phải tương ứng với RouteVersion hiện tại
        if (latestAssessment.RouteVersion != route.Version)
        {
            throw new ForbiddenException(
                $"Bản đánh giá rủi ro của tuyến đường '{route.Id}' (RouteVersion={latestAssessment.RouteVersion}) " +
                $"đã lỗi thời so với phiên bản dữ liệu hiện tại (RouteVersion={route.Version}). " +
                $"Vui lòng yêu cầu phân tích/khuyến nghị lại trước khi kích hoạt.");
        }

        // 3. Bảo vệ Policy Version: Đánh giá phải được thực hiện với PolicyVersion hiện hành
        if (latestAssessment.PolicyId != effectivePolicy.PolicyId ||
            latestAssessment.PolicyVersion != effectivePolicy.Version)
        {
            throw new ForbiddenException(
                $"Bản đánh giá rủi ro của tuyến đường '{route.Id}' " +
                $"(Policy '{latestAssessment.PolicyId}' v{latestAssessment.PolicyVersion}) đã lỗi thời do chính sách rủi ro đã thay đổi " +
                $"(hiện tại: '{effectivePolicy.PolicyId}' v{effectivePolicy.Version}). " +
                $"Bắt buộc chạy phân tích rủi ro lại theo chính sách mới.");
        }

        // 4. Kiểm tra quyết định vận hành
        if (route.GovernanceDecision == GovernanceDecision.Blocked ||
            latestAssessment.GovernanceDecision == GovernanceDecision.Blocked)
        {
            throw new ForbiddenException(
                $"Tuyến đường '{route.Id}' bị CHẶN thực thi do rủi ro mức CRITICAL: {latestAssessment.ReasonDetails}");
        }

        if (route.GovernanceDecision == GovernanceDecision.ManagerApprovalRequired)
        {
            // Kiểm tra phê duyệt còn hiệu lực của Manager
            var approvals = await context.ApprovalRequests
                .AsNoTracking()
                .Where(a => a.RouteId == route.Id &&
                            a.Status == ApprovalStatus.Approved &&
                            a.RouteVersion == route.Version &&
                            a.PolicyId == effectivePolicy.PolicyId &&
                            a.PolicyVersion == effectivePolicy.Version)
                .ToListAsync(ct);

            var validApproval = approvals
                .OrderByDescending(a => a.ReviewedAt ?? a.CreatedAt)
                .FirstOrDefault();

            if (validApproval is null)
            {
                throw new ForbiddenException(
                    $"Tuyến đường '{route.Id}' có mức độ rủi ro '{route.RiskLevel}' và yêu cầu Manager phê duyệt. " +
                    $"Chưa có phê duyệt hợp lệ của Quản lý cho phiên bản RouteVersion={route.Version} và PolicyVersion={effectivePolicy.Version}.");
            }
        }
    }
}
