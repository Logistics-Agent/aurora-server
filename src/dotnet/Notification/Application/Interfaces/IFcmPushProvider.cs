using Notification.Domain.Entities;

namespace Notification.Application.Interfaces;

public sealed record FcmMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);
public enum FcmSendStatus { Sent, TransientFailure, InvalidToken, PermanentFailure }
public sealed record FcmSendResult(FcmSendStatus Status, string? ProviderMessageId = null, string? ErrorCode = null);

public interface IFcmPushProvider
{
    Task<FcmSendResult> SendAsync(NotificationDevice device, FcmMessage message, CancellationToken cancellationToken);
}
