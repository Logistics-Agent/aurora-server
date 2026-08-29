using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Domain.Enums;
using Shared.Enums;
using Shared.Rules;
using Route = RoutePlanningAgent.Domain.Route;

namespace RoutePlanningAgent.Application.Interfaces;

public record RiskAssessmentResult
{
    public RouteRiskLevel RiskLevel { get; init; }
    public GovernanceDecision Decision { get; init; }
    public string Source { get; init; } = "DeterministicRules";
    public string PolicyId { get; init; } = "platform-default-route-governance";
    public int PolicyVersion { get; init; } = 1;
    public RiskPolicySource PolicySource { get; init; } = RiskPolicySource.PlatformDefault;
    public IReadOnlyList<string> MatchedRuleCodes { get; init; } = [];
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public string ReasonDetails { get; init; } = string.Empty;
    public double? RiskScore { get; init; }
    public double? ConfidenceScore { get; init; }
    public bool RequiresManagerApproval => Decision == GovernanceDecision.ManagerApprovalRequired;
    public bool IsBlocked => Decision == GovernanceDecision.Blocked;
    public IReadOnlyList<RuleResult> RuleResults { get; init; } = [];
}

public interface IRouteGovernanceService
{
    /// <summary>
    /// Đánh giá rủi ro đa chiều dựa trên Effective Risk Policy đã phân giải,
    /// kết hợp kết quả Rule Engine, Compliance RAG và AI Recommendation.
    /// Ghi nhận nguồn gốc chính sách (PolicyId, PolicyVersion, PolicySource).
    /// </summary>
    Task<RiskAssessmentResult> AssessRouteAsync(
        Route route,
        EffectiveRiskPolicy effectivePolicy,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult = null,
        RouteAiResult? aiResult = null,
        CancellationToken ct = default);

    /// <summary>
    /// Đánh giá rủi ro cho thao tác Soft Delete dựa trên tác động nghiệp vụ của trạng thái Route.
    /// </summary>
    RiskAssessmentResult AssessSoftDeleteRisk(
        Route route,
        EffectiveRiskPolicy effectivePolicy);

    /// <summary>
    /// Kiểm tra quyền thực thi (Execution Boundary).
    /// Xác thực toàn diện:
    /// - Route.Version khớp với bản đánh giá rủi ro mới nhất
    /// - PolicyVersion/PolicyId của bản đánh giá khớp với Effective Policy hiện tại
    /// - Nếu yêu cầu Manager duyệt: Phê duyệt phải còn hiệu lực trên đúng RouteVersion và PolicyVersion hiện tại.
    /// </summary>
    Task ValidateExecutionAuthorizedAsync(
        Route route,
        EffectiveRiskPolicy effectivePolicy,
        CancellationToken ct = default);
}
