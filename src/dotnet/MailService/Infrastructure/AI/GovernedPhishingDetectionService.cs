using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Security;
using MailService.Application.Interfaces.AI;

namespace MailService.Infrastructure.AI;

public class GovernedPhishingDetectionService : IPhishingDetectionService
{
    private readonly IAiGovernanceClient _governanceClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GovernedPhishingDetectionService> _logger;

    public GovernedPhishingDetectionService(
        IAiGovernanceClient governanceClient,
        ICurrentUserService currentUserService,
        ILogger<GovernedPhishingDetectionService> logger)
    {
        _governanceClient = governanceClient;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<(decimal PhishingScore, string Reasoning)> AnalyzePhishingAsync(
        string subject,
        string body,
        string sender,
        List<string> urls,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = _currentUserService.TenantId ?? Guid.Empty;

        // AI Governance Policy Pre-Check
        var policy = await _governanceClient.ExecutePolicyAsync(tenantId, "PhishingDetection", cancellationToken);
        if (!policy.IsAllowed || policy.SkipAi)
        {
            _logger.LogInformation("AI Phishing detection skipped by AI Governance policy for tenant {TenantId}. Reason: {Reason}", tenantId, policy.Reason);
            return (0.0m, $"Skipped by policy: {policy.Reason}");
        }

        try
        {
            string prompt = $$"""
            Analyze the following inbound email for phishing, credential harvesting, or social engineering attacks.
            Sender: {{sender}}
            Subject: {{subject}}
            URLs: {{string.Join(", ", urls)}}
            Body:
            {{body}}

            Respond ONLY in JSON format: {"phishingScore": <0.0 to 1.0>, "reasoning": "<string>"}
            """;

            string systemInstruction = "You are an automated email cybersecurity analyst for Aurora Mail Platform.";
            string responseJson = await _governanceClient.GenerateAsync(tenantId, prompt, systemInstruction, cancellationToken);

            using var doc = JsonDocument.Parse(responseJson);
            decimal score = doc.RootElement.GetProperty("phishingScore").GetDecimal();
            string reasoning = doc.RootElement.GetProperty("reasoning").GetString() ?? "Analyzed by AI Governance";

            return (score, reasoning);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze phishing via AI Governance for tenant {TenantId}. Falling back safely.", tenantId);
            return (0.0m, "Fail-safe: AI analysis unavailable");
        }
    }
}
