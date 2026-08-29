using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Firebase;

public sealed class FirebaseAdminInitializer(IOptions<FirebaseOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.Enabled
            && string.IsNullOrWhiteSpace(settings.CredentialsPath)
            && !settings.HasInlineCredentials)
            throw new InvalidOperationException("Firebase is enabled but credentials are missing.");
        if (settings.Enabled
            && !string.IsNullOrWhiteSpace(settings.CredentialsPath)
            && !File.Exists(settings.CredentialsPath))
            throw new InvalidOperationException("Firebase credentials file was not found.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
