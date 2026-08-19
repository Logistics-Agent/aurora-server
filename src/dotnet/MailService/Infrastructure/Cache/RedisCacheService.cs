using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using MailService.Application.Interfaces;

namespace MailService.Infrastructure.Cache;

public class RedisCacheService : IRateLimitService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer? redis = null, ILogger<RedisCacheService>? logger = null)
    {
        _redis = redis;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisCacheService>.Instance;
    }

    public async Task<bool> IsMessageIdDuplicateAsync(Guid tenantId, string messageId, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return false;

        try
        {
            var db = _redis.GetDatabase();
            string key = $"message-id:{tenantId}:{messageId}";
            bool isSet = await db.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists);
            return !isSet; // If SETNX returned false, key already existed -> duplicate!
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SETNX duplicate message check failed.");
            return false;
        }
    }

    public async Task<bool> IsInboundRateExceededAsync(Guid tenantId, string senderAddress, int maxPerMinute, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return false;

        try
        {
            var db = _redis.GetDatabase();
            string key = $"rate:inbound:{tenantId}:{senderAddress}";
            long count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(60));
            }
            return count > maxPerMinute;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis inbound rate limit check failed.");
            return false;
        }
    }

    public async Task<(bool Exceeded, long CurrentCount, DateTimeOffset ResetTime)> IsOutboundRateExceededAsync(
        Guid tenantId,
        Guid mailboxId,
        int maxPerHour,
        CancellationToken cancellationToken = default)
    {
        var resetTime = DateTimeOffset.UtcNow.AddHours(1);
        if (_redis == null || !_redis.IsConnected) return (false, 1, resetTime);

        try
        {
            var db = _redis.GetDatabase();
            string key = $"rate:outbound:{tenantId}:{mailboxId}";
            long count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
            }

            return (count > maxPerHour, count, resetTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis outbound rate limit check failed.");
            return (false, 1, resetTime);
        }
    }
}
