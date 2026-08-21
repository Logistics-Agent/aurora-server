namespace MailService.Infrastructure.AI;

public class AiGovernanceOptions
{
    public const string SectionName = "AiGovernance";
    public string GrpcEndpoint { get; set; } = "http://localhost:5005";
    public int TimeoutSeconds { get; set; } = 3;
    public int MaxRetryAttempts { get; set; } = 2;
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
