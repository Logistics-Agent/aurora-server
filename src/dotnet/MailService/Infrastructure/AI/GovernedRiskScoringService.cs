using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Security;
using MailService.Application.Interfaces.AI;

namespace MailService.Infrastructure.AI;

public class GovernedRiskScoringService : IRiskScoringService
{
    private readonly IAiGovernanceClient _governanceClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GovernedRiskScoringService> _logger;

    public GovernedRiskScoringService(
        IAiGovernanceClient governanceClient,
        ICurrentUserService currentUserService,
        ILogger<GovernedRiskScoringService> logger)
    {
        _governanceClient = governanceClient;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<(decimal RiskScore, string Reasoning)> AnalyzeBecRiskAsync(
        string sender,
        List<string> recipients,
        string subject,
        string bodyText,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = _currentUserService.TenantId ?? Guid.Empty;

        // AI Governance Policy Pre-Check
        var policy = await _governanceClient.ExecutePolicyAsync(tenantId, "OutboundBecScoring", cancellationToken);
        if (!policy.IsAllowed || policy.SkipAi)
        {
            _logger.LogInformation("Outbound BEC risk scoring skipped by AI Governance policy for tenant {TenantId}. Reason: {Reason}", tenantId, policy.Reason);
            return (0.0m, $"Skipped by policy: {policy.Reason}");
        }

        try
        {
            string prompt = $$"""
            Analyze the following outbound email for Business Email Compromise (BEC), unauthorized wire transfer requests, or executive impersonation.
            Sender: {{sender}}
            Recipients: {{string.Join(", ", recipients)}}
            Subject: {{subject}}
            Body:
            {{bodyText}}

            Respond ONLY in JSON format: {"riskScore": <0.0 to 1.0>, "reasoning": "<string>"}
            """;

            string systemInstruction = "You are an outbound email security analyst for Aurora Mail Platform.";
            string responseJson = await _governanceClient.GenerateAsync(tenantId, prompt, systemInstruction, cancellationToken);

            using var doc = JsonDocument.Parse(responseJson);
            decimal score = doc.RootElement.GetProperty("riskScore").GetDecimal();
            string reasoning = doc.RootElement.GetProperty("reasoning").GetString() ?? "Analyzed by AI Governance";

            return (score, reasoning);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze outbound BEC risk via AI Governance for tenant {TenantId}. Falling back safely.", tenantId);
            return (0.0m, "Fail-safe: AI analysis unavailable");
        }
    }
}
