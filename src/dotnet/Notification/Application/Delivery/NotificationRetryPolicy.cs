namespace Notification.Application.Delivery;

public sealed class NotificationRetryOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan MaximumDelay { get; init; } = TimeSpan.FromMinutes(5);
}

public sealed record NotificationRetryDecision(
    bool ShouldRetry,
    DateTimeOffset? NextAttemptAt);

public interface INotificationRetryPolicy
{
    NotificationRetryDecision Decide(
        int attemptNumber,
        bool isTransient,
        DateTimeOffset failedAt);
}

public sealed class NotificationRetryPolicy : INotificationRetryPolicy
{
    private readonly NotificationRetryOptions _options;

    public NotificationRetryPolicy(NotificationRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxAttempts must be positive.");
        if (options.InitialDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "InitialDelay cannot be negative.");
        if (options.MaximumDelay < options.InitialDelay)
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumDelay cannot be shorter than InitialDelay.");

        _options = options;
    }

    public NotificationRetryDecision Decide(
        int attemptNumber,
        bool isTransient,
        DateTimeOffset failedAt)
    {
        if (attemptNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        if (failedAt == default)
            throw new ArgumentException("FailedAt is required.", nameof(failedAt));

        if (!isTransient || attemptNumber >= _options.MaxAttempts)
            return new NotificationRetryDecision(false, null);

        var exponent = Math.Min(attemptNumber - 1, 20);
        var delayTicks = _options.InitialDelay.Ticks;
        for (var index = 0; index < exponent && delayTicks < _options.MaximumDelay.Ticks; index++)
        {
            delayTicks = delayTicks > _options.MaximumDelay.Ticks / 2
                ? _options.MaximumDelay.Ticks
                : Math.Min(delayTicks * 2, _options.MaximumDelay.Ticks);
        }
        var nextAttemptAt = failedAt.AddTicks(delayTicks);

        return new NotificationRetryDecision(true, nextAttemptAt);
    }
}
