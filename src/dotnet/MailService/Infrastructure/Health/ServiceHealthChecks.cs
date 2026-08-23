using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace MailService.Infrastructure.Health;

public class StalwartHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly ILogger<StalwartHealthCheck> _logger;

    public StalwartHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<StalwartHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["Stalwart:BaseUrl"] ?? "http://localhost:8080";
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            var uri = new Uri(new Uri(_baseUrl), "/healthz");
            var response = await client.GetAsync(uri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Stalwart is reachable at {_baseUrl}");
            }

            return HealthCheckResult.Unhealthy($"Stalwart returned status code {response.StatusCode} from {uri}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart health probe failed for {BaseUrl}", _baseUrl);
            return HealthCheckResult.Unhealthy($"Stalwart unreachable at {_baseUrl}: {ex.Message}");
        }
    }
}

public class ClamAvHealthCheck : IHealthCheck
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<ClamAvHealthCheck> _logger;

    public ClamAvHealthCheck(IConfiguration configuration, ILogger<ClamAvHealthCheck> logger)
    {
        _host = configuration["ClamAV:Host"] ?? "clamav";
        _port = int.TryParse(configuration["ClamAV:Port"], out int p) ? p : 3310;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await client.ConnectAsync(_host, _port, cts.Token);
            return HealthCheckResult.Healthy($"ClamAV daemon reachable at {_host}:{_port}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClamAV health probe failed for {Host}:{Port}", _host, _port);
            return HealthCheckResult.Unhealthy($"ClamAV daemon unreachable at {_host}:{_port}: {ex.Message}");
        }
    }
}

public class SpamAssassinHealthCheck : IHealthCheck
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<SpamAssassinHealthCheck> _logger;

    public SpamAssassinHealthCheck(IConfiguration configuration, ILogger<SpamAssassinHealthCheck> logger)
    {
        _host = configuration["SpamAssassin:Host"] ?? "spamassassin";
        _port = int.TryParse(configuration["SpamAssassin:Port"], out int p) ? p : 783;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await client.ConnectAsync(_host, _port, cts.Token);
            return HealthCheckResult.Healthy($"SpamAssassin daemon reachable at {_host}:{_port}");
        }
        catch (Exception ex)
        {
            // SpamAssassin is non-blocking degraded dependency
            _logger.LogWarning(ex, "SpamAssassin health probe degraded for {Host}:{Port}", _host, _port);
            return HealthCheckResult.Degraded($"SpamAssassin daemon unreachable at {_host}:{_port}: {ex.Message}");
        }
    }
}

public class AiGovernanceHealthCheck : IHealthCheck
{
    private readonly string _grpcEndpoint;
    private readonly ILogger<AiGovernanceHealthCheck> _logger;

    public AiGovernanceHealthCheck(IConfiguration configuration, ILogger<AiGovernanceHealthCheck> logger)
    {
        _grpcEndpoint = configuration["AiGovernance:GrpcEndpoint"]
            ?? configuration["AiGovernance:ServiceUrl"]
            ?? "http://localhost:5005";
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse URI for endpoint validation
            if (!Uri.TryCreate(_grpcEndpoint, UriKind.Absolute, out var uri))
            {
                return HealthCheckResult.Degraded($"Invalid AI Governance endpoint URI: {_grpcEndpoint}");
            }

            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await client.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 5005, cts.Token);
            return HealthCheckResult.Healthy($"AI Governance gRPC reachable at {_grpcEndpoint}");
        }
        catch (Exception ex)
        {
            // AI Governance is governed by Polly fail-safe (degraded, not critical for process survival)
            _logger.LogInformation("AI Governance health probe degraded for {Endpoint}: {Message}", _grpcEndpoint, ex.Message);
            return HealthCheckResult.Degraded($"AI Governance gRPC degraded at {_grpcEndpoint}: {ex.Message}");
        }
    }
}
