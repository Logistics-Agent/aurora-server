using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Infrastructure.Services;

/// <summary>
/// Cache-aside cho ngưỡng rule per-tenant, dùng GENERATION KEY để hỗ trợ wildcard invalidation:
/// - Key dữ liệu:  tenant-rule-config:{tenantId}:{gen}:{ruleName}
/// - Key gen:      tenant-rule-config-gen:{tenantId}
/// InvalidateCacheAsync(tenantId, "") tăng gen → toàn bộ key cũ thành "mồ côi" (TTL 1h tự dọn).
/// </summary>
public class TenantRuleConfigService(
    RoutePlanningDbContext context,
    IDistributedCache cache)
    : ITenantRuleConfigService
{
    private static readonly DistributedCacheEntryOptions CacheOptions =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) };

    private static string GenKey(Guid tenantId) => $"tenant-rule-config-gen:{tenantId}";

    private static string DataKey(Guid tenantId, long gen, string ruleName)
        => $"tenant-rule-config:{tenantId}:{gen}:{ruleName}";

    public async Task<TenantRuleThresholds> GetThresholdsAsync(
        Guid tenantId, string ruleName, CancellationToken ct = default)
    {
        var gen = await GetGenerationAsync(tenantId, ct);
        var key = DataKey(tenantId, gen, ruleName);

        // 1. Cache Aside — Check Redis
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<TenantRuleThresholds>(cached)!;

        // 2. Cache miss — Load from DB
        var config = await context.TenantRuleConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                   && c.RuleName == ruleName, ct);

        TenantRuleThresholds thresholds;

        if (config is not null)
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(config.ThresholdsJson)
                         ?? [];
            thresholds = new TenantRuleThresholds
            {
                IsEnabled = config.IsEnabled,
                Values = values
            };
        }
        else
        {
            thresholds = new TenantRuleThresholds
            {
                IsEnabled = true,
                Values = []
            };
        }

        // 3. Set Redis — TTL 1 hour
        await cache.SetStringAsync(key, JsonSerializer.Serialize(thresholds), CacheOptions, ct);

        return thresholds;
    }

    public async Task InvalidateCacheAsync(Guid tenantId, string ruleName, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ruleName))
        {
            // Wildcard: tăng generation → mọi key cũ của tenant bị bỏ qua, TTL 1h tự dọn
            var gen = await GetGenerationAsync(tenantId, ct);
            await cache.SetStringAsync(GenKey(tenantId), (gen + 1).ToString(), ct);
            return;
        }

        var currentGen = await GetGenerationAsync(tenantId, ct);
        await cache.RemoveAsync(DataKey(tenantId, currentGen, ruleName), ct);
    }

    private async Task<long> GetGenerationAsync(Guid tenantId, CancellationToken ct)
    {
        var raw = await cache.GetStringAsync(GenKey(tenantId), ct);
        return long.TryParse(raw, out var gen) ? gen : 1;
    }
}
