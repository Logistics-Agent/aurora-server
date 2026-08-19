using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces;

namespace MailService.Infrastructure.AI;

public class SemanticKernelPhishingService : IPhishingDetectionService
{
    private readonly ILogger<SemanticKernelPhishingService> _logger;

    public SemanticKernelPhishingService(ILogger<SemanticKernelPhishingService> logger)
    {
        _logger = logger;
    }

    public async Task<(decimal PhishingScore, string Reasoning)> AnalyzePhishingAsync(
        string subject,
        string body,
        string sender,
        List<string> urls,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        // Heuristic analysis fallback / LLM integration simulation
        decimal score = 0.05m;
        string reasoning = "Clean text structure and normal URL pattern.";

        string lowerText = $"{subject} {body}".ToLowerInvariant();
        if (lowerText.Contains("urgent password reset") || lowerText.Contains("account suspended") || lowerText.Contains("verify banking details"))
        {
            score = 0.85m;
            reasoning = "Suspicious high-urgency credential harvesting phrases detected.";
        }

        _logger.LogInformation("Phishing analysis completed. Score: {Score}, Reason: {Reason}", score, reasoning);
        return (score, reasoning);
    }
}

public class SemanticKernelRiskScoringService
{
    private readonly ILogger<SemanticKernelRiskScoringService> _logger;

    public SemanticKernelRiskScoringService(ILogger<SemanticKernelRiskScoringService> logger)
    {
        _logger = logger;
    }

    public async Task<(decimal RiskScore, string Reasoning)> AnalyzeBecRiskAsync(
        string subject,
        string body,
        string sender,
        List<string> recipients,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        decimal score = 0.0m;
        string reasoning = "Standard operational outbound email.";

        string lowerText = $"{subject} {body}".ToLowerInvariant();
        if (lowerText.Contains("wire transfer") || lowerText.Contains("change bank account details") || lowerText.Contains("urgent payment required"))
        {
            score = 0.75m;
            reasoning = "Outbound BEC risk detected: sensitive financial transfer instruction.";
        }

        return (score, reasoning);
    }
}
