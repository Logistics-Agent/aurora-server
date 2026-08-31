using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Firebase;

public sealed class FirebaseConfigurationHealthCheck(
    IOptions<FirebaseOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Firebase delivery is disabled."));

        if (!settings.HasInlineCredentials && string.IsNullOrWhiteSpace(settings.CredentialsPath))
            return Task.FromResult(HealthCheckResult.Unhealthy("Firebase credentials are not configured."));

        if (!string.IsNullOrWhiteSpace(settings.CredentialsPath) && !File.Exists(settings.CredentialsPath))
            return Task.FromResult(HealthCheckResult.Unhealthy("Firebase credential file is unavailable."));

        return Task.FromResult(HealthCheckResult.Healthy("Firebase configuration is available."));
    }
}
