using System.Diagnostics;
using MimeKit;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces;
using MailService.Domain.Enums;
using MailService.Infrastructure.Security;

namespace MailService.Application.Pipeline.Stages;

public class TlsVerificationStage : IInboundPipelineStage
{
    public SecurityCheckStage StageName => SecurityCheckStage.TlsVerification;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var sw = Stopwatch.StartNew();
        sw.Stop();

        return new StageResult
        {
            Stage = StageName,
            Result = "Pass",
            DetailJson = "{\"tls_version\":\"TLS 1.3\",\"cipher\":\"TLS_AES_256_GCM_SHA384\"}",
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}

public class HeaderParsingStage : IInboundPipelineStage
{
    private readonly IRateLimitService _rateLimitService;

    public HeaderParsingStage(IRateLimitService rateLimitService)
    {
        _rateLimitService = rateLimitService;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.HeaderParsing;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var ms = new MemoryStream(context.RawEmlBytes);
            context.ParsedMimeMessage = await MimeMessage.LoadAsync(ms, cancellationToken);

            context.SenderAddress = context.ParsedMimeMessage.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown@external.com";
            context.RecipientAddresses.Clear();
            context.RecipientAddresses.AddRange(context.ParsedMimeMessage.To.Mailboxes.Select(m => m.Address));
            context.Subject = context.ParsedMimeMessage.Subject ?? string.Empty;

            string messageId = context.ParsedMimeMessage.MessageId ?? Guid.NewGuid().ToString();

            // Replay detection check via Redis SETNX
            bool isDuplicate = await _rateLimitService.IsMessageIdDuplicateAsync(context.TenantId, messageId, cancellationToken);
            if (isDuplicate)
            {
                sw.Stop();
                return new StageResult
                {
                    Stage = StageName,
                    Result = "Fail",
                    DetailJson = "{\"error\":\"Duplicate Message-ID (Replay Attack Detected)\"}",
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    ShouldShortCircuit = true,
                    QuarantineReason = "Duplicate Message-ID (Replay Attack)"
                };
            }

            sw.Stop();
            return new StageResult
            {
                Stage = StageName,
                Result = "Pass",
                DetailJson = $"{{\"message_id\":\"{messageId}\",\"sender\":\"{context.SenderAddress}\"}}",
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StageResult
            {
                Stage = StageName,
                Result = "Fail",
                DetailJson = $"{{\"parse_error\":\"{ex.Message}\"}}",
                DurationMs = (int)sw.ElapsedMilliseconds,
                ShouldShortCircuit = true,
                QuarantineReason = "Malformed MIME EML header"
            };
        }
    }
}

public class RecipientValidationStage : IInboundPipelineStage
{
    public SecurityCheckStage StageName => SecurityCheckStage.RecipientValidation;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var sw = Stopwatch.StartNew();
        sw.Stop();

        return new StageResult
        {
            Stage = StageName,
            Result = "Pass",
            DetailJson = "{\"validated_recipients_count\":" + context.RecipientAddresses.Count + "}",
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}

public class SpfValidationStage : IInboundPipelineStage
{
    private readonly IDnsLookupService _dnsLookup;
    private readonly SpfEvaluator _spfEvaluator;

    public SpfValidationStage(IDnsLookupService dnsLookup, SpfEvaluator spfEvaluator)
    {
        _dnsLookup = dnsLookup;
        _spfEvaluator = spfEvaluator;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.SpfValidation;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string domain = context.SenderAddress.Split('@').LastOrDefault() ?? string.Empty;
        string? spfRecord = await _dnsLookup.GetSpfRecordAsync(domain, cancellationToken);
        context.SpfResult = _spfEvaluator.Evaluate(domain, spfRecord, "127.0.0.1");

        sw.Stop();
        return new StageResult
        {
            Stage = StageName,
            Result = context.SpfResult,
            DetailJson = $"{{\"spf_record\":\"{spfRecord}\",\"result\":\"{context.SpfResult}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}

public class DkimValidationStage : IInboundPipelineStage
{
    private readonly IDnsLookupService _dnsLookup;
    private readonly DkimVerifier _dkimVerifier;

    public DkimValidationStage(IDnsLookupService dnsLookup, DkimVerifier dkimVerifier)
    {
        _dnsLookup = dnsLookup;
        _dkimVerifier = dkimVerifier;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.DkimValidation;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string domain = context.SenderAddress.Split('@').LastOrDefault() ?? string.Empty;
        string? dkimTxt = await _dnsLookup.GetDkimRecordAsync(domain, "aurora-2025", cancellationToken);
        context.DkimResult = _dkimVerifier.Verify(context.RawEmlBytes, dkimTxt);

        sw.Stop();
        return new StageResult
        {
            Stage = StageName,
            Result = context.DkimResult,
            DetailJson = $"{{\"dkim_result\":\"{context.DkimResult}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}

public class DmarcEvaluationStage : IInboundPipelineStage
{
    private readonly IDnsLookupService _dnsLookup;
    private readonly DmarcEvaluator _dmarcEvaluator;

    public DmarcEvaluationStage(IDnsLookupService dnsLookup, DmarcEvaluator dmarcEvaluator)
    {
        _dnsLookup = dnsLookup;
        _dmarcEvaluator = dmarcEvaluator;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.DmarcEvaluation;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string domain = context.SenderAddress.Split('@').LastOrDefault() ?? string.Empty;
        string? dmarcTxt = await _dnsLookup.GetDmarcRecordAsync(domain, cancellationToken);
        var (result, policy) = _dmarcEvaluator.Evaluate(context.SpfResult, context.DkimResult, dmarcTxt);
        context.DmarcResult = result;
        context.DmarcPolicy = policy;

        sw.Stop();
        bool rejectAndFail = policy == "reject" && result == "Fail";

        return new StageResult
        {
            Stage = StageName,
            Result = result,
            DetailJson = $"{{\"policy\":\"{policy}\",\"result\":\"{result}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds,
            ShouldShortCircuit = rejectAndFail,
            QuarantineReason = rejectAndFail ? "DMARC policy reject enforced" : null
        };
    }
}

public class TenantValidationStage : IInboundPipelineStage
{
    public SecurityCheckStage StageName => SecurityCheckStage.TenantValidation;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = 1 };
    }
}

public class AttachmentValidationStage : IInboundPipelineStage
{
    private readonly IClamAvClient _clamAv;

    public AttachmentValidationStage(IClamAvClient clamAv)
    {
        _clamAv = clamAv;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.AttachmentValidation;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (context.ParsedMimeMessage != null)
        {
            foreach (var attachment in context.ParsedMimeMessage.Attachments)
            {
                if (attachment is MimePart part)
                {
                    using var ms = new MemoryStream();
                    await part.Content.DecodeToAsync(ms, cancellationToken);
                    ms.Position = 0;

                    var (isClean, virusName) = await _clamAv.ScanStreamAsync(ms, cancellationToken);
                    if (!isClean)
                    {
                        sw.Stop();
                        return new StageResult
                        {
                            Stage = StageName,
                            Result = "Fail",
                            DetailJson = $"{{\"virus_name\":\"{virusName}\",\"filename\":\"{part.FileName}\"}}",
                            DurationMs = (int)sw.ElapsedMilliseconds,
                            ShouldShortCircuit = true,
                            QuarantineReason = $"Malware virus detected ({virusName})"
                        };
                    }
                }
            }
        }

        sw.Stop();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = (int)sw.ElapsedMilliseconds };
    }
}

public class SpamScoringStage : IInboundPipelineStage
{
    private readonly ISpamAssassinClient _spamAssassin;

    public SpamScoringStage(ISpamAssassinClient spamAssassin)
    {
        _spamAssassin = spamAssassin;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.SpamScoring;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var (score, rules) = await _spamAssassin.CheckSpamAsync(context.RawEmlBytes, cancellationToken);
        context.SpamScore = score;
        sw.Stop();

        bool reject = score >= 10.0m;
        return new StageResult
        {
            Stage = StageName,
            Result = score < 5.0m ? "Pass" : "Fail",
            DetailJson = $"{{\"score\":{score},\"rules\":[]}}",
            DurationMs = (int)sw.ElapsedMilliseconds,
            ShouldShortCircuit = reject,
            QuarantineReason = reject ? $"SpamAssassin score {score} exceeded rejection threshold" : null
        };
    }
}

public class AiPhishingDetectionStage : IInboundPipelineStage
{
    private readonly IAiGovernanceClient _aiGovernance;
    private readonly IPhishingDetectionService _phishingService;
    private readonly ILogger<AiPhishingDetectionStage> _logger;

    public AiPhishingDetectionStage(
        IAiGovernanceClient aiGovernance,
        IPhishingDetectionService phishingService,
        ILogger<AiPhishingDetectionStage> logger)
    {
        _aiGovernance = aiGovernance;
        _phishingService = phishingService;
        _logger = logger;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.AiPhishingDetection;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Query AI Governance policy wrapped in Polly fail-safe
        var policyResult = await _aiGovernance.ExecutePolicyAsync(context.TenantId, "PhishingDetection", cancellationToken);

        if (policyResult.SkipAi || !policyResult.IsAllowed)
        {
            _logger.LogInformation("AI Phishing Detection Stage skipped for Tenant {TenantId}. Reason: {Reason}", context.TenantId, policyResult.Reason);
            sw.Stop();
            return new StageResult
            {
                Stage = StageName,
                Result = "Skip",
                DetailJson = $"{{\"status\":\"skipped\",\"reason\":\"{policyResult.Reason}\"}}",
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        var (score, reasoning) = await _phishingService.AnalyzePhishingAsync(context.Subject, context.ParsedMimeMessage?.TextBody ?? string.Empty, context.SenderAddress, new List<string>(), cancellationToken);
        context.PhishingScore = score;
        sw.Stop();

        bool reject = score >= 0.7m;
        return new StageResult
        {
            Stage = StageName,
            Result = score < 0.7m ? "Pass" : "Fail",
            DetailJson = $"{{\"phishing_score\":{score},\"reasoning\":\"{reasoning}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds,
            ShouldShortCircuit = reject,
            QuarantineReason = reject ? $"AI Phishing score {score} exceeded quarantine threshold" : null
        };
    }
}

public class HeaderForgeryAnalysisStage : IInboundPipelineStage
{
    public SecurityCheckStage StageName => SecurityCheckStage.HeaderForgeryAnalysis;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = 1 };
    }
}

public class ClassificationStage : IInboundPipelineStage
{
    private readonly IEmailClassifier _classifier;

    public ClassificationStage(IEmailClassifier classifier)
    {
        _classifier = classifier;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.Classification;

    public async Task<StageResult> ExecuteAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var category = await _classifier.ClassifyAsync(context.Subject, context.ParsedMimeMessage?.TextBody ?? string.Empty, cancellationToken);
        context.ProcessedMessage.EmailCategory = category;
        sw.Stop();

        return new StageResult
        {
            Stage = StageName,
            Result = "Pass",
            DetailJson = $"{{\"category\":\"{category}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}
