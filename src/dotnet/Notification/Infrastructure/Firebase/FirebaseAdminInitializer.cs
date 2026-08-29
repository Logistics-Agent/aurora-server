using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Firebase;

public sealed class FirebaseAdminInitializer(IOptions<FirebaseOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled && (string.IsNullOrWhiteSpace(options.Value.ProjectId) || string.IsNullOrWhiteSpace(options.Value.ClientEmail) || string.IsNullOrWhiteSpace(options.Value.PrivateKey)))
            throw new InvalidOperationException("Firebase is enabled but credentials are missing.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
