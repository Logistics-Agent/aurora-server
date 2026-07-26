using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Infrastructure.Services;

public class TenantRuleConfigService(
    RoutePlanningDbContext context,
    IDistributedCache cache)
    : ITenantRuleConfigService
{
    private static string CacheKey(Guid tenantId, string ruleName)
        => $"tenant-rule-config:{tenantId}:{ruleName}";

    public async Task<TenantRuleThresholds> GetThresholdsAsync(
        Guid tenantId, string ruleName, CancellationToken ct = default)
    {
        var key = CacheKey(tenantId, ruleName);

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
        await cache.SetStringAsync(key,
            JsonSerializer.Serialize(thresholds),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
            ct);

        return thresholds;
    }

    public async Task InvalidateCacheAsync(Guid tenantId, string ruleName, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ruleName))
        {
            // Invalidate all rules for tenant.
            // Since we can't easily perform wildcard deletes in IDistributedCache,
            // we will require ruleName for granular cache invalidation or handle ruleName = empty.
            // If empty, let's do nothing, or we can handle it at a higher level.
            // For standard MassTransit event, the publisher provides the specific ruleName or we can do nothing.
            // Let's check ruleName is empty, if so, we can't do wildcard without direct Redis connection.
            // Since this is standard IDistributedCache, we can log a warning or skip.
            return;
        }

        await cache.RemoveAsync(CacheKey(tenantId, ruleName), ct);
    }
}
