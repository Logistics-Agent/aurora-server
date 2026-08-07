using MimeKit;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Pipeline;

public class StageResult
{
    public SecurityCheckStage Stage {; set; }
    public string Result {; set; } = "Pass"; // Pass, Fail, Skip, Error
    public string? DetailJson {; set; }
    public int DurationMs {; set; }
    public bool ShouldShortCircuit {; set; }
    public string? QuarantineReason {; set; }
}

public class InboundPipelineContext
{
    public PipelineExecutionId ExecutionId {; } = PipelineExecutionId.New();
    public Guid TenantId {; set; }
    public byte[] RawEmlBytes {; set; } = Array.Empty<byte>();
    public MimeMessage? ParsedMimeMessage {; set; }
    public ProcessedMessage ProcessedMessage {; set; } = new();
    public List<StageResult> StageResults {; } = new();
    public string SenderAddress {; set; } = string.Empty;
    public List<string> RecipientAddresses {; } = new();
    public string Subject {; set; } = string.Empty;
    public decimal SpamScore {; set; }
    public decimal PhishingScore {; set; }
    public string SpfResult {; set; } = "None";
    public string DkimResult {; set; } = "None";
    public string DmarcResult {; set; } = "None";
    public string DmarcPolicy {; set; } = "none";
    public bool IsQuarantined {; set; }
    public string? QuarantineReason {; set; }
}

public class OutboundPipelineContext
{
    public PipelineExecutionId ExecutionId {; } = PipelineExecutionId.New();
    public Guid TenantId {; set; }
    public string SenderAddress {; set; } = string.Empty;
    public List<string> RecipientAddresses {; } = new();
    public string Subject {; set; } = string.Empty;
    public string BodyText {; set; } = string.Empty;
    public string BodyHtml {; set; } = string.Empty;
    public List<(string Filename, string ContentType, byte[] Content)> Attachments {; } = new();
    public Guid? DraftRootId {; set; }
    public Guid? FinalDraftRevisionId {; set; }
    public DraftSource DraftSource {; set; } = DraftSource.Manual;
    public ProcessedMessage ProcessedMessage {; set; } = new();
    public List<StageResult> StageResults {; } = new();
    public string? StalwartQueueId {; set; }
    public bool IsRejected {; set; }
    public string? RejectionReason {; set; }
}

public interface IInboundPipelineStage
{
    SecurityCheckStage StageName {; }
    Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default);
}

public interface IOutboundPipelineStage
{
    SecurityCheckStage StageName {; }
    Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default);
}
