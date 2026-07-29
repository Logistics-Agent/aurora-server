using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Xunit;

namespace RoutePlanningAgent.Tests.Services;

public class TenantRuleConfigServiceTests
{
    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task CacheMiss_DocTuDb_RoiCacheLai()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        context.TenantRuleConfigs.Add(new TenantRuleConfig
        {
            TenantId = TestDb.TenantId,
            RuleName = "HeavyWeightRule",
            IsEnabled = true,
            ThresholdsJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["maxWeightKg"] = 2000m })
        });
        await context.SaveChangesAsync();

        var service = new TenantRuleConfigService(context, NewCache());

        var thresholds = await service.GetThresholdsAsync(TestDb.TenantId, "HeavyWeightRule");

        Assert.True(thresholds.IsEnabled);
        Assert.Equal(2000m, thresholds.Get("maxWeightKg", 5000m));
    }

    [Fact]
    public async Task KhongCoConfig_TraVeDefault_Enabled()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var service = new TenantRuleConfigService(context, NewCache());
        var thresholds = await service.GetThresholdsAsync(TestDb.TenantId, "HeavyWeightRule");

        Assert.True(thresholds.IsEnabled);
        Assert.Equal(5000m, thresholds.Get("maxWeightKg", 5000m)); // dùng global default
    }

    [Fact]
    public async Task InvalidateRuleCuThe_LanSauDocLaiTuDb()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var config = new TenantRuleConfig
        {
            TenantId = TestDb.TenantId,
            RuleName = "HeavyWeightRule",
            ThresholdsJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["maxWeightKg"] = 2000m })
        };
        context.TenantRuleConfigs.Add(config);
        await context.SaveChangesAsync();

        var service = new TenantRuleConfigService(context, NewCache());

        // Warm cache với giá trị cũ
        await service.GetThresholdsAsync(TestDb.TenantId, "HeavyWeightRule");

        // Đổi DB + invalidate rule cụ thể
        config.ThresholdsJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["maxWeightKg"] = 3000m });
        await context.SaveChangesAsync();
        await service.InvalidateCacheAsync(TestDb.TenantId, "HeavyWeightRule");

        var thresholds = await service.GetThresholdsAsync(TestDb.TenantId, "HeavyWeightRule");
        Assert.Equal(3000m, thresholds.Get("maxWeightKg", 5000m));
    }

    [Fact]
    public async Task InvalidateWildcard_GenerationKey_MoiRuleDeuRefresh()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var config = new TenantRuleConfig
        {
            TenantId = TestDb.TenantId,
            RuleName = "LargeVolumeRule",
            ThresholdsJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["maxVolumeM3"] = 40m })
        };
        context.TenantRuleConfigs.Add(config);
        await context.SaveChangesAsync();

        var service = new TenantRuleConfigService(context, NewCache());

        // Warm cache
        await service.GetThresholdsAsync(TestDb.TenantId, "LargeVolumeRule");

        // Đổi DB + invalidate WILDCARD (ruleName rỗng — trước đây là no-op, giờ dùng generation key)
        config.ThresholdsJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["maxVolumeM3"] = 99m });
        await context.SaveChangesAsync();
        await service.InvalidateCacheAsync(TestDb.TenantId, "");

        var thresholds = await service.GetThresholdsAsync(TestDb.TenantId, "LargeVolumeRule");
        Assert.Equal(99m, thresholds.Get("maxVolumeM3", 50m));
    }
}
