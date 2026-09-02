using Microsoft.Extensions.Options;
using System.Text.Json;

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
        if (settings.Enabled && !string.IsNullOrWhiteSpace(settings.CredentialsPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(settings.CredentialsPath));
                var root = document.RootElement;
                if (!root.TryGetProperty("project_id", out var projectId) ||
                    string.IsNullOrWhiteSpace(projectId.GetString()) ||
                    !root.TryGetProperty("client_email", out var clientEmail) ||
                    string.IsNullOrWhiteSpace(clientEmail.GetString()) ||
                    !root.TryGetProperty("private_key", out var privateKey) ||
                    string.IsNullOrWhiteSpace(privateKey.GetString()))
                    throw new InvalidOperationException("Firebase credential file is missing project identity.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Firebase credential file is not valid JSON.", ex);
            }
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
