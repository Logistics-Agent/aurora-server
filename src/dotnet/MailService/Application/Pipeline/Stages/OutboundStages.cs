using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Security;
using Shared.Constants;
using MailService.Application.Interfaces.AI;
using MailService.Application.Interfaces.RateLimiting;
using MailService.Application.Interfaces.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Enums;
using MailService.Infrastructure.AI;

namespace MailService.Application.Pipeline.Stages;

public class OutboundAttachmentValidationStage : IOutboundPipelineStage
{
    private readonly IClamAvClient _clamAv;

    public OutboundAttachmentValidationStage(IClamAvClient clamAv)
    {
        _clamAv = clamAv;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.OutboundAttachmentValidation;

    public async Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        foreach (var attachment in context.Attachments)
        {
            using var ms = new MemoryStream(attachment.Content);
            var scanResult = await _clamAv.ScanStreamAsync(ms, cancellationToken);
            if (!scanResult.IsClean)
            {
                sw.Stop();
                string reason = scanResult.Status == ClamAvStatus.ServiceUnavailable
                    ? "Outbound attachment scan unavailable (ClamAV down) - deferring send"
                    : $"Outbound attachment infected: {scanResult.VirusName}";

                context.IsRejected = true;
                context.RejectionReason = reason;
                return new StageResult
                {
                    Stage = StageName,
                    Result = "Fail",
                    DetailJson = $"{{\"virus_name\":\"{scanResult.VirusName}\",\"filename\":\"{attachment.Filename}\",\"status\":\"{scanResult.Status}\"}}",
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    ShouldShortCircuit = true
                };
            }
        }

        sw.Stop();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = (int)sw.ElapsedMilliseconds };
    }
}

public class PolicyValidationStage : IOutboundPipelineStage
{
    private readonly ICurrentUserService _currentUserService;

    public PolicyValidationStage(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.PolicyValidation;

    public async Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Enforce role authorization: reject AI Agent / Service Account calling SubmitOutboundMessage directly
        var role = _currentUserService.Role;
        bool isStaffOrAdmin = role == RoleConstants.Staff || role == RoleConstants.Manager || role == RoleConstants.TenantAdmin || role == RoleConstants.SystemAdmin;
        bool isAiAgentServiceAccount = string.Equals(role, "AiAgent", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "ServiceAccount", StringComparison.OrdinalIgnoreCase);

        if (isAiAgentServiceAccount && !isStaffOrAdmin)
        {
            sw.Stop();
            context.IsRejected = true;
            context.RejectionReason = "PERMISSION_DENIED: Service-Account / AI Agent JWTs cannot directly submit outbound messages. Use CreateDraftMessage instead.";

            return new StageResult
            {
                Stage = StageName,
                Result = "Fail",
                DetailJson = "{\"error\":\"PERMISSION_DENIED: AI Agent direct send blocked\"}",
                DurationMs = (int)sw.ElapsedMilliseconds,
                ShouldShortCircuit = true
            };
        }

        sw.Stop();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = (int)sw.ElapsedMilliseconds };
    }
}

public class AiRiskScoringStage : IOutboundPipelineStage
{
    private readonly IAiGovernanceClient _aiGovernance;
    private readonly IRiskScoringService _riskScoringService;
    private readonly ILogger<AiRiskScoringStage> _logger;

    public AiRiskScoringStage(
        IAiGovernanceClient aiGovernance,
        IRiskScoringService riskScoringService,
        ILogger<AiRiskScoringStage> logger)
    {
        _aiGovernance = aiGovernance;
        _riskScoringService = riskScoringService;
        _logger = logger;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.AiRiskScoring;

    public async Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Query AI Governance policy wrapped in Polly fail-safe
        var policyResult = await _aiGovernance.ExecutePolicyAsync(context.TenantId, "BecRiskScoring", cancellationToken);

        if (policyResult.SkipAi || !policyResult.IsAllowed)
        {
            _logger.LogInformation("AI Risk Scoring Stage skipped for Tenant {TenantId}. Reason: {Reason}", context.TenantId, policyResult.Reason);
            sw.Stop();
            return new StageResult
            {
                Stage = StageName,
                Result = "Skip",
                DetailJson = $"{{\"status\":\"skipped\",\"reason\":\"{policyResult.Reason}\"}}",
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        var (riskScore, reasoning) = await _riskScoringService.AnalyzeBecRiskAsync(context.SenderAddress, context.RecipientAddresses, context.Subject, context.BodyText, cancellationToken);
        sw.Stop();

        return new StageResult
        {
            Stage = StageName,
            Result = "Pass",
            DetailJson = $"{{\"bec_risk_score\":{riskScore},\"reasoning\":\"{reasoning}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds
        };
    }
}

public class RateLimitCheckStage : IOutboundPipelineStage
{
    private readonly IRateLimitService _rateLimitService;

    public RateLimitCheckStage(IRateLimitService rateLimitService)
    {
        _rateLimitService = rateLimitService;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.RateLimitCheck;

    public async Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var (exceeded, count, resetTime) = await _rateLimitService.IsOutboundRateExceededAsync(context.TenantId, Guid.Empty, 200, cancellationToken);

        if (exceeded)
        {
            sw.Stop();
            context.IsRejected = true;
            context.RejectionReason = $"RESOURCE_EXHAUSTED: Outbound rate limit of 200/hr exceeded (Current: {count}). Reset at {resetTime:O}";

            return new StageResult
            {
                Stage = StageName,
                Result = "Fail",
                DetailJson = $"{{\"error\":\"RESOURCE_EXHAUSTED\",\"count\":{count}}}",
                DurationMs = (int)sw.ElapsedMilliseconds,
                ShouldShortCircuit = true
            };
        }

        sw.Stop();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = (int)sw.ElapsedMilliseconds };
    }
}

public class AuditCreationStage : IOutboundPipelineStage
{
    public SecurityCheckStage StageName => SecurityCheckStage.AuditCreation;

    public async Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return new StageResult { Stage = StageName, Result = "Pass", DurationMs = 1 };
    }
}

public class StalwartSmtpSubmissionStage : IOutboundPipelineStage
{
    private readonly ISmtpDeliveryService _smtpDeliveryService;
    private readonly ILogger<StalwartSmtpSubmissionStage> _logger;

    public StalwartSmtpSubmissionStage(ISmtpDeliveryService smtpDeliveryService, ILogger<StalwartSmtpSubmissionStage> logger)
    {
        _smtpDeliveryService = smtpDeliveryService;
        _logger = logger;
    }

    public SecurityCheckStage StageName => SecurityCheckStage.StalwartSmtpSubmission;

    public async Task<StageResult> ExecuteAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var deliveryResult = await _smtpDeliveryService.DeliverAsync(
            context.SenderAddress,
            context.RecipientAddresses,
            context.Subject,
            context.BodyText,
            context.BodyHtml,
            context.Attachments,
            cancellationToken);

        sw.Stop();

        if (deliveryResult.IsSuccess)
        {
            context.StalwartQueueId = deliveryResult.QueueId;
            return new StageResult
            {
                Stage = StageName,
                Result = "Pass",
                DetailJson = $"{{\"status\":\"success\",\"status_code\":{deliveryResult.StatusCode},\"queue_id\":\"{deliveryResult.QueueId}\",\"message\":\"{deliveryResult.StatusMessage}\"}}",
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        // Delivery failed
        context.IsRejected = true;
        context.RejectionReason = $"SMTP Delivery Failure ({deliveryResult.Status} - Code {deliveryResult.StatusCode}): {deliveryResult.StatusMessage}";

        _logger.LogWarning("SMTP delivery failed with status {Status} ({StatusCode}): {Message}",
            deliveryResult.Status, deliveryResult.StatusCode, deliveryResult.StatusMessage);

        return new StageResult
        {
            Stage = StageName,
            Result = "Fail",
            DetailJson = $"{{\"status\":\"{deliveryResult.Status}\",\"status_code\":{deliveryResult.StatusCode},\"error\":\"{deliveryResult.StatusMessage}\"}}",
            DurationMs = (int)sw.ElapsedMilliseconds,
            ShouldShortCircuit = true
        };
    }
}
