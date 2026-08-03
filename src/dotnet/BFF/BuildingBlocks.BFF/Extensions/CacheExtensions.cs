using Microsoft.Extensions.DependencyInjection;
using Shared.Cache;
using Shared.Extensions;

namespace BuildingBlocks.BFF.Extensions;

public static class CacheExtensions
{
    /// <summary>
    /// Đăng ký Redis distributed cache + PermissionCacheService.
    /// </summary>
    public static IServiceCollection AddBffCache(
        this IServiceCollection services,
        IConfiguration config)
    {
        var redisConn = SharedServiceExtensions.BuildRedisConnectionString(config);

        services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);
        services.AddScoped<IPermissionCacheService, PermissionCacheService>();

        return services;
    }

    /// <summary>
    /// Lấy Redis connection string đã được validate.
    /// Dùng cho Health Checks registration cần biết connection string.
    /// </summary>
    public static string GetRedisConnectionString(IConfiguration config) =>
        SharedServiceExtensions.BuildRedisConnectionString(config);
}
