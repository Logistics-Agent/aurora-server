namespace MailService.Application.Interfaces.AI;

public record AiGovernancePolicyResult(bool IsAllowed, string ProviderType, bool SkipAi, string Reason)
{
    public static AiGovernancePolicyResult Allowed(string provider) => new(true, provider, false, "Allowed by policy");
    public static AiGovernancePolicyResult Denied(string reason) => new(false, "None", true, reason);
    public static AiGovernancePolicyResult FallbackSkipAi(string reason) => new(false, "None", true, reason);
}

public interface IAiGovernanceClient
{
    Task<AiGovernancePolicyResult> ExecutePolicyAsync(Guid tenantId, string policyName, CancellationToken cancellationToken = default);
    Task<string> GenerateAsync(Guid tenantId, string prompt, string systemInstruction = "", CancellationToken cancellationToken = default);
}
