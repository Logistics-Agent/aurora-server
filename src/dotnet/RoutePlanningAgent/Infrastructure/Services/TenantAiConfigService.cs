using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;

namespace RoutePlanningAgent.Infrastructure.Services;

public class TenantAiConfigService(
    RoutePlanningDbContext context,
    IDistributedCache cache)
    : ITenantAiConfigService
{
    private static string CacheKey(Guid tenantId, string feature)
        => $"tenant-ai-config:{tenantId}:{feature}";

    public async Task<TenantAiConfig?> GetConfigAsync(
        Guid tenantId, string feature, CancellationToken ct = default)
    {
        var key = CacheKey(tenantId, feature);

        // 1. Cache Aside — Check Redis
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<TenantAiConfig>(cached);

        // 2. Cache miss — Load from DB
        var config = await context.TenantAiConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId
                                   && c.Feature == feature
                                   && c.IsActive, ct);

        // 3. Set Redis — TTL 1 hour
        if (config is not null)
        {
            await cache.SetStringAsync(key,
                JsonSerializer.Serialize(config),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
                ct);
        }

        return config;
    }

    public async Task<AutomationPolicy> GetPolicyAsync(Guid tenantId, string feature, CancellationToken ct = default)
    {
        var config = await GetConfigAsync(tenantId, feature, ct);
        return config?.Policy ?? AutomationPolicy.RulesOnly; // Default to RulesOnly if not configured
    }

    public async Task InvalidateCacheAsync(Guid tenantId, string feature, CancellationToken ct = default)
    {
        await cache.RemoveAsync(CacheKey(tenantId, feature), ct);
    }
}
