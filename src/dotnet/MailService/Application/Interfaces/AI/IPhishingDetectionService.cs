namespace MailService.Application.Interfaces.AI;

public interface IPhishingDetectionService
{
    Task<(decimal PhishingScore, string Reasoning)> AnalyzePhishingAsync(
        string subject,
        string body,
        string sender,
        List<string> urls,
        CancellationToken cancellationToken = default);
}

public interface IRiskScoringService
{
    Task<(decimal RiskScore, string Reasoning)> AnalyzeBecRiskAsync(
        string sender,
        List<string> recipients,
        string subject,
        string bodyText,
        CancellationToken cancellationToken = default);
}
