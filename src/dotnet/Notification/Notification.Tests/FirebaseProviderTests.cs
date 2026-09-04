using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Firebase;
using Xunit;

namespace Notification.Tests;

public sealed class FirebaseProviderTests
{
    [Fact]
    public async Task Disabled_firebase_returns_permanent_failure_without_credentials()
    {
        var provider = new FirebasePushProvider(
            Options.Create(new FirebaseOptions { Enabled = false }),
            NullLogger<FirebasePushProvider>.Instance);

        var result = await provider.SendAsync(
            NotificationDevice.Register(Guid.NewGuid(), Guid.NewGuid(), "token", DevicePlatform.Web),
            new FcmMessage("Title", "Body", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(FcmSendStatus.PermanentFailure, result.Status);
        Assert.Equal("firebase_disabled", result.ErrorCode);
    }

    [Fact]
    public void Enabled_firebase_requires_a_credentials_source()
    {
        Assert.Throws<InvalidOperationException>(() => new FirebasePushProvider(
            Options.Create(new FirebaseOptions { Enabled = true }),
            NullLogger<FirebasePushProvider>.Instance));
    }
}
