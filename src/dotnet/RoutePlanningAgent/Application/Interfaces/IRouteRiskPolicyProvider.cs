using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Enums;

namespace RoutePlanningAgent.Application.Interfaces;

public record EffectiveRiskPolicy
{
    public string PolicyId { get; init; } = "platform-default-route-governance";
    public int Version { get; init; } = 1;
    public RiskPolicySource Source { get; init; } = RiskPolicySource.PlatformDefault;
    public string Scope { get; init; } = "RoutePlanning";
    public Guid? TenantId { get; init; }
    public IReadOnlyDictionary<string, TenantRuleThresholds> RuleThresholds { get; init; } =
        new Dictionary<string, TenantRuleThresholds>();
}

public interface IRouteRiskPolicyProvider
{
    /// <summary>
    /// Phân giải chính sách rủi ro hiệu lực (Effective Risk Policy) cho tenant.
    /// Thứ tự ưu tiên:
    /// 1. TENANT CUSTOM POLICY (nếu tenant cấu hình CUSTOM và policy khả dụng)
    /// 2. PLATFORM DEFAULT (nếu tenant cấu hình DEFAULT hoặc chưa có custom policy)
    /// 
    /// QUY TẮC AN TOÀN: Nếu tenant cấu hình CUSTOM nhưng truy xuất policy thất bại,
    /// phương thức ném PolicyUnavailableException và TUYỆT ĐỐI KHÔNG âm thầm fallback về default.
    /// </summary>
    Task<EffectiveRiskPolicy> GetEffectivePolicyAsync(
        Guid tenantId,
        string scope = "RoutePlanning",
        CancellationToken ct = default);
}
