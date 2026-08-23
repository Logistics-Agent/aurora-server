using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.BFF.Mail.Models;

namespace BuildingBlocks.BFF.Mail.Clients;

public interface IMailServiceClient
{
    // Management
    Task<ProvisionDomainResponse> ProvisionDomainAsync(ProvisionDomainRequest request, CancellationToken cancellationToken = default);
    Task<CreateMailboxResponse> CreateMailboxAsync(CreateMailboxRequest request, CancellationToken cancellationToken = default);
    Task<CreateAliasResponse> CreateAliasAsync(CreateAliasRequest request, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponse> ResetPasswordAsync(string mailboxId, CancellationToken cancellationToken = default);
    Task<AuditListResponse> GetAuditRecordsAsync(string? resourceType, string? resourceId, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default);
    Task<RequeueDeadLetterResponse> RequeueDeadLetterAsync(string processedMessageId, CancellationToken cancellationToken = default);

    // Security & Operations
    Task<DraftResponse> CreateDraftAsync(CreateDraftRequest request, CancellationToken cancellationToken = default);
    Task<DraftListResponse> ListDraftsAsync(string? mailboxId, string? status, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default);
    Task<DraftResponse> GetDraftAsync(string draftId, CancellationToken cancellationToken = default);
    Task<SubmitOutboundMessageResponse> SubmitOutboundMessageAsync(SubmitOutboundMessageRequest request, CancellationToken cancellationToken = default);
    Task<ProcessedMessageResponse> GetProcessedMessageAsync(string processedMessageId, CancellationToken cancellationToken = default);
    Task<ProcessedMessageListResponse> ListProcessedMessagesAsync(string? direction, string? emailCategory, string? pipelineStatus, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default);
    Task<QuarantineRecordResponse> GetQuarantineRecordAsync(string quarantineId, CancellationToken cancellationToken = default);
    Task<QuarantineListResponse> ListQuarantineRecordsAsync(string? status, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default);
    Task<ReleaseQuarantineResponse> ReleaseQuarantineAsync(string quarantineId, CancellationToken cancellationToken = default);
    Task<DeleteQuarantineResponse> DeleteQuarantineAsync(string quarantineId, CancellationToken cancellationToken = default);
}
