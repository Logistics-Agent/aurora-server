using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RoutePlanningAgent.Application.Interfaces;

public interface ITenantRuleConfigService
{
    Task<TenantRuleThresholds> GetThresholdsAsync(
        Guid tenantId, string ruleName, CancellationToken ct = default);
    Task InvalidateCacheAsync(Guid tenantId, string ruleName, CancellationToken ct = default);
}

public record TenantRuleThresholds
{
    public bool IsEnabled { get; init; } = true;
    public Dictionary<string, decimal> Values { get; init; } = [];

    public decimal Get(string key, decimal globalDefault)
        => Values.TryGetValue(key, out var val) ? val : globalDefault;
}
