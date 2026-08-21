namespace MailService.Application.Interfaces.RateLimiting;

public interface IRateLimitService
{
    Task<bool> IsInboundRateExceededAsync(Guid tenantId, string senderAddress, int maxPerMinute, CancellationToken cancellationToken = default);
    Task<(bool Exceeded, long CurrentCount, DateTimeOffset ResetTime)> IsOutboundRateExceededAsync(Guid tenantId, Guid mailboxId, int maxPerHour, CancellationToken cancellationToken = default);
    Task<bool> IsMessageIdDuplicateAsync(Guid tenantId, string messageId, CancellationToken cancellationToken = default);
}
