using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using AiGovernance.Grpc;
using MailService.Application.Interfaces.AI;
using Shared.Security;

namespace MailService.Infrastructure.AI;

public class AiGovernanceGrpcClient : IAiGovernanceClient
{
    private readonly AiGovernanceService.AiGovernanceServiceClient _policyClient;
    private readonly AiExecutionService.AiExecutionServiceClient _executionClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AiGovernanceGrpcClient> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public AiGovernanceGrpcClient(
        AiGovernanceService.AiGovernanceServiceClient? policyClient = null,
        AiExecutionService.AiExecutionServiceClient? executionClient = null,
        ICurrentUserService? currentUserService = null,
        IOptions<AiGovernanceOptions>? options = null,
        ILogger<AiGovernanceGrpcClient>? logger = null)
    {
        _policyClient = policyClient!;
        _executionClient = executionClient!;
        _currentUserService = currentUserService ?? new CurrentUserService();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AiGovernanceGrpcClient>.Instance;

        var opt = options?.Value ?? new AiGovernanceOptions();

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(opt.TimeoutSeconds > 0 ? opt.TimeoutSeconds : 3))
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = opt.MaxRetryAttempts > 0 ? opt.MaxRetryAttempts : 2,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = opt.CircuitBreakerFailureRatio > 0 ? opt.CircuitBreakerFailureRatio : 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(opt.CircuitBreakerDurationSeconds > 0 ? opt.CircuitBreakerDurationSeconds : 30)
            })
            .Build();
    }

    private Metadata CreateHeaders(Guid tenantId)
    {
        var headers = new Metadata
        {
            { "x-service-id", "mail-service" },
            { "x-tenant-id", tenantId.ToString() }
        };

        if (_currentUserService.UserId.HasValue)
        {
            headers.Add("x-user-id", _currentUserService.UserId.Value.ToString());
        }

        return headers;
    }

    public async Task<AiGovernancePolicyResult> ExecutePolicyAsync(Guid tenantId, string policyName, CancellationToken cancellationToken = default)
    {
        if (_policyClient == null)
        {
            return AiGovernancePolicyResult.FallbackSkipAi("AI Governance policy client is not configured.");
        }

        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var headers = CreateHeaders(tenantId);
                var request = new ExecutePolicyRequest
                {
                    CapabilityCode = policyName,
                    EstimatedInputTokens = 500,
                    MaxOutputTokens = 500
                };

                var response = await _policyClient.ExecutePolicyAsync(request, headers, cancellationToken: ct);

                if (!response.Allowed)
                {
                    return AiGovernancePolicyResult.Denied(response.DenyReason);
                }

                var primaryProvider = response.AllowedProviders.FirstOrDefault() ?? "AI-Governance";
                return AiGovernancePolicyResult.Allowed(primaryProvider);
            }, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "AI Governance circuit broken for Tenant {TenantId}. Fail-safe fallback to SkipAi.", tenantId);
            return AiGovernancePolicyResult.FallbackSkipAi("AI Governance circuit broken - fail-safe activated");
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "AI Governance call timed out for Tenant {TenantId}. Fail-safe fallback to SkipAi.", tenantId);
            return AiGovernancePolicyResult.FallbackSkipAi("AI Governance call timed out - fail-safe activated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI Governance unavailable for Tenant {TenantId}. Fail-safe fallback to SkipAi.", tenantId);
            return AiGovernancePolicyResult.FallbackSkipAi($"AI Governance service error ({ex.Message}) - fail-safe activated");
        }
    }

    public async Task<string> GenerateAsync(Guid tenantId, string prompt, string systemInstruction = "", CancellationToken cancellationToken = default)
    {
        if (_executionClient == null)
        {
            throw new InvalidOperationException("AI Execution Client is not configured.");
        }

        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var headers = CreateHeaders(tenantId);
                var request = new AiGenerateRequest
                {
                    CapabilityCode = "mail_security",
                    Prompt = string.IsNullOrEmpty(systemInstruction) ? prompt : $"{systemInstruction}\n\n{prompt}",
                    MaxOutputTokens = 1000
                };

                var response = await _executionClient.GenerateAsync(request, headers, cancellationToken: ct);
                return response.Content ?? string.Empty;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Governance Generate call failed for Tenant {TenantId}", tenantId);
            throw;
        }
    }
}
