using MimeKit;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Pipeline;

public class StageResult
{
    public SecurityCheckStage Stage { get; set; }
    public string Result { get; set; } = "Pass"; // Pass, Fail, Skip, Error
    public string? DetailJson { get; set; }
    public int DurationMs { get; set; }
    public bool ShouldShortCircuit { get; set; }
    public string? QuarantineReason { get; set; }
}

public class InboundPipelineContext
{
    public PipelineExecutionId ExecutionId { get; } = PipelineExecutionId.New();
    public Guid TenantId { get; set; }
    public byte[] RawEmlBytes { get; set; } = Array.Empty<byte>();
    public MimeMessage? ParsedMimeMessage { get; set; }
    public ProcessedMessage ProcessedMessage { get; set; } = new();
    public List<StageResult> StageResults { get; } = new();
    public string SenderAddress { get; set; } = string.Empty;
    public List<string> RecipientAddresses { get; } = new();
    public string Subject { get; set; } = string.Empty;
    public decimal SpamScore { get; set; }
    public decimal PhishingScore { get; set; }
    public string SpfResult { get; set; } = "None";
    public string DkimResult { get; set; } = "None";
    public string DmarcResult { get; set; } = "None";
    public string DmarcPolicy { get; set; } = "none";
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
}

public class OutboundPipelineContext
{
    public PipelineExecutionId ExecutionId { get; } = PipelineExecutionId.New();
    public Guid TenantId { get; set; }
    public string SenderAddress { get; set; } = string.Empty;
    public List<string> RecipientAddresses { get; } = new();
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public List<(string Filename, string ContentType, byte[] Content)> Attachments { get; } = new();
    public Guid? DraftRootId { get; set; }
    public Guid? FinalDraftRevisionId { get; set; }
    public DraftSource DraftSource { get; set; } = DraftSource.Manual;
    public ProcessedMessage ProcessedMessage { get; set; } = new();
    public List<StageResult> StageResults { get; } = new();
    public string? StalwartQueueId { get; set; }
    public bool IsRejected { get; set; }
    public string? RejectionReason { get; set; }
}

public interface IInboundPipelineStage
{
    SecurityCheckStage StageName { get; }
    Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default);
}

public interface IOutboundPipelineStage
{
    SecurityCheckStage StageName { get; }
    Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default);
}
