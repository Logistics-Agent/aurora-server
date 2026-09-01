using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Firebase;

public sealed class FakeFcmPushProvider : IFcmPushProvider
{
    public List<(NotificationDevice Device, FcmMessage Message)> SentMessages { get; } = [];
    public FcmSendStatus NextStatus { get; set; } = FcmSendStatus.Sent;

    public Task<FcmSendResult> SendAsync(NotificationDevice device, FcmMessage message, CancellationToken cancellationToken)
    {
        SentMessages.Add((device, message));
        return Task.FromResult(new FcmSendResult(NextStatus, NextStatus == FcmSendStatus.Sent ? "fake-message" : null, NextStatus.ToString()));
    }
}
