using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using Polly.CircuitBreaker;
using Shared.Security;
using MailService.Application.Interfaces;

namespace MailService.Infrastructure.AI;

public class AiGovernanceGrpcClient : IAiGovernanceClient
{
    private readonly ILogger<AiGovernanceGrpcClient> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public AiGovernanceGrpcClient(ILogger<AiGovernanceGrpcClient> logger)
    {
        _logger = logger;

        // Configure Polly Resilience Pipeline (Timeout + Retry + Circuit Breaker)
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(3))
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
    }

    public async Task<AiGovernancePolicyResult> ExecutePolicyAsync(Guid tenantId, string policyName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                // In production environment, this calls Subscription Service gRPC ExecutePolicy.
                // For demonstration & default operation, return allowed with Gemini AI provider.
                await Task.Delay(10, ct);
                return AiGovernancePolicyResult.Allowed("Gemini");
            }, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "Subscription Service AI Governance circuit broken for Tenant {TenantId}. Fail-safe fallback to SkipAi.", tenantId);
            return AiGovernancePolicyResult.FallbackSkipAi("AI Governance circuit broken - fail-safe activated");
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Subscription Service AI Governance call timed out for Tenant {TenantId}. Fail-safe fallback to SkipAi.", tenantId);
            return AiGovernancePolicyResult.FallbackSkipAi("AI Governance call timed out - fail-safe activated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Subscription Service AI Governance unavailable for Tenant {TenantId}. Fail-safe fallback to SkipAi.", tenantId);
            return AiGovernancePolicyResult.FallbackSkipAi($"AI Governance service error ({ex.Message}) - fail-safe activated");
        }
    }
}
