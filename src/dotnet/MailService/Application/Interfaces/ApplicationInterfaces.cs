using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Interfaces;

public interface IStalwartManagementClient
{
    Task<bool> RegisterDomainAsync(string domainName, CancellationToken cancellationToken = default);
    Task<string> GenerateDkimKeyAsync(string domainName, string selector = "aurora-2025", CancellationToken cancellationToken = default);
    Task<bool> ProvisionAccountAsync(string fullAddress, CancellationToken cancellationToken = default);
    Task<byte[]> GetMessageEmlAsync(string messageId, CancellationToken cancellationToken = default);
    Task<bool> DeliverQuarantinedMessageAsync(string messageId, string recipientAddress, CancellationToken cancellationToken = default);
}

public interface IClamAvClient
{
    Task<(bool IsClean, string VirusName)> ScanStreamAsync(Stream stream, CancellationToken cancellationToken = default);
}

public interface ISpamAssassinClient
{
    Task<(decimal Score, List<string> Rules)> CheckSpamAsync(byte[] emlBytes, CancellationToken cancellationToken = default);
}

public interface IR2StorageClient
{
    Task<string> UploadRawEmlAsync(Guid tenantId, string messageId, EmailDirection direction, byte[] emlBytes, CancellationToken cancellationToken = default);
    Task<string> UploadAttachmentAsync(Guid tenantId, string messageId, EmailDirection direction, string filename, byte[] content, CancellationToken cancellationToken = default);
    Task<string> UploadJsonMetadataAsync(Guid tenantId, string messageId, EmailDirection direction, string keySuffix, string jsonContent, CancellationToken cancellationToken = default);
    Task<string> GeneratePresignedUrlAsync(string objectKey, int expirySeconds = 3600, CancellationToken cancellationToken = default);
}

public interface IDnsLookupService
{
    Task<string?> GetSpfRecordAsync(string domain, CancellationToken cancellationToken = default);
    Task<string?> GetDkimRecordAsync(string domain, string selector, CancellationToken cancellationToken = default);
    Task<string?> GetDmarcRecordAsync(string domain, CancellationToken cancellationToken = default);
}

public interface IPhishingDetectionService
{
    Task<(decimal PhishingScore, string Reasoning)> AnalyzePhishingAsync(string subject, string body, string sender, List<string> urls, CancellationToken cancellationToken = default);
}

public record AiGovernancePolicyResult(bool IsAllowed, string ProviderType, bool SkipAi, string Reason)
{
    public static AiGovernancePolicyResult Allowed(string provider) => new(true, provider, false, "Allowed by policy");
    public static AiGovernancePolicyResult Denied(string reason) => new(false, "None", true, reason);
    public static AiGovernancePolicyResult FallbackSkipAi(string reason) => new(false, "None", true, reason);
}

public interface IAiGovernanceClient
{
    Task<AiGovernancePolicyResult> ExecutePolicyAsync(Guid tenantId, string policyName, CancellationToken cancellationToken = default);
}

public interface IEmailDraftRepository
{
    Task<EmailDraft?> GetLatestRevisionAsync(Guid draftRootId, CancellationToken cancellationToken = default);
    Task<EmailDraft> CreateNewDraftAsync(EmailDraft draft, CancellationToken cancellationToken = default);
    Task<EmailDraft> CreateNextRevisionInTransactionAsync(Guid draftRootId, string subject, string body, DraftSource source, DraftStatus status, Guid mailboxId, Guid? assignedStaffId, CancellationToken cancellationToken = default);
    Task MarkAsSentAsync(Guid draftRootId, CancellationToken cancellationToken = default);
}

public interface IEmailClassifier
{
    Task<EmailCategory> ClassifyAsync(string subject, string body, CancellationToken cancellationToken = default);
}

public interface IRateLimitService
{
    Task<bool> IsInboundRateExceededAsync(Guid tenantId, string senderAddress, int maxPerMinute, CancellationToken cancellationToken = default);
    Task<(bool Exceeded, long CurrentCount, DateTimeOffset ResetTime)> IsOutboundRateExceededAsync(Guid tenantId, Guid mailboxId, int maxPerHour, CancellationToken cancellationToken = default);
    Task<bool> IsMessageIdDuplicateAsync(Guid tenantId, string messageId, CancellationToken cancellationToken = default);
}
