using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;
using Shared.Exceptions;

namespace RoutePlanningAgent.Infrastructure.Services;

public class RouteRiskPolicyProvider(
    RoutePlanningDbContext context,
    ITenantRuleConfigService ruleConfigService)
    : IRouteRiskPolicyProvider
{
    public const string PlatformDefaultPolicyId = "platform-default-route-governance";
    public const int PlatformDefaultVersion = 1;

    public async Task<EffectiveRiskPolicy> GetEffectivePolicyAsync(
        Guid tenantId,
        string scope = "RoutePlanning",
        CancellationToken ct = default)
    {
        TenantRiskPolicyConfig? config;
        try
        {
            config = await context.TenantRiskPolicyConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        }
        catch (Exception ex)
        {
            throw new PolicyUnavailableException(
                $"Truy xuất cấu hình chính sách rủi ro của Tenant '{tenantId}' thất bại: {ex.Message}");
        }

        // 1. UNCONFIGURED -> Chặn tuyệt đối (Block), ném RiskPolicyNotConfiguredException
        if (config == null)
        {
            throw new RiskPolicyNotConfiguredException(
                $"Tenant '{tenantId}' chưa thiết lập cấu hình chính sách rủi ro (Risk Policy). " +
                $"Vui lòng cấu hình tường minh 'UsePlatformDefault' hoặc 'UseCustomPolicy' trước khi thực hiện vận hành.");
        }

        // 2. USE_PLATFORM_DEFAULT -> Áp dụng Platform Default Policy v1
        if (config.PolicyMode == RiskPolicyMode.UsePlatformDefault)
        {
            var policyId = !string.IsNullOrWhiteSpace(config.ActivePolicyId)
                ? config.ActivePolicyId
                : PlatformDefaultPolicyId;
            var policyVersion = config.ActivePolicyVersion > 0
                ? config.ActivePolicyVersion
                : PlatformDefaultVersion;

            return new EffectiveRiskPolicy
            {
                PolicyId = policyId,
                Version = policyVersion,
                Source = RiskPolicySource.PlatformDefault,
                Scope = scope,
                TenantId = tenantId,
                RuleThresholds = new Dictionary<string, TenantRuleThresholds>()
            };
        }

        // 3. USE_CUSTOM_POLICY -> Áp dụng Tenant Policy
        if (config.PolicyMode == RiskPolicyMode.UseCustomPolicy)
        {
            try
            {
                var ruleConfigs = await context.TenantRuleConfigs
                    .AsNoTracking()
                    .Where(r => r.TenantId == tenantId)
                    .ToListAsync(ct);

                var thresholdsMap = new Dictionary<string, TenantRuleThresholds>();
                foreach (var rc in ruleConfigs)
                {
                    var thresholds = await ruleConfigService.GetThresholdsAsync(tenantId, rc.RuleName, ct);
                    thresholdsMap[rc.RuleName] = thresholds;
                }

                var policyId = !string.IsNullOrWhiteSpace(config.ActivePolicyId)
                    ? config.ActivePolicyId
                    : $"tenant-policy-{tenantId}";
                var policyVersion = config.ActivePolicyVersion > 0
                    ? config.ActivePolicyVersion
                    : 1;

                return new EffectiveRiskPolicy
                {
                    PolicyId = policyId,
                    Version = policyVersion,
                    Source = RiskPolicySource.Tenant,
                    Scope = scope,
                    TenantId = tenantId,
                    RuleThresholds = thresholdsMap
                };
            }
            catch (Exception ex) when (ex is not PolicyUnavailableException)
            {
                // QUY TẮC AN TOÀN: Tuyệt đối KHÔNG âm thầm fallback về Platform Default khi Custom Policy gặp lỗi
                throw new PolicyUnavailableException(
                    $"Không thể tải các quy tắc tuỳ chỉnh cho Tenant '{tenantId}' (PolicyMode=UseCustomPolicy): {ex.Message}");
            }
        }

        throw new RiskPolicyNotConfiguredException(
            $"Chế độ chính sách rủi ro '{config.PolicyMode}' của Tenant '{tenantId}' không được hỗ trợ.");
    }
}
