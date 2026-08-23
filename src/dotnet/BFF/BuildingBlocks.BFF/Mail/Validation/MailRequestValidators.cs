using System;
using System.Text.RegularExpressions;
using FluentValidation;
using BuildingBlocks.BFF.Mail.Models;

namespace BuildingBlocks.BFF.Mail.Validation;

public class ProvisionDomainRequestValidator : AbstractValidator<ProvisionDomainRequest>
{
    private static readonly Regex FqdnRegex = new(@"^(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.[A-Za-z0-9-]{1,63})*\.[A-Za-z]{2,6}$", RegexOptions.Compiled);

    public ProvisionDomainRequestValidator()
    {
        RuleFor(x => x.DomainName)
            .NotEmpty().WithMessage("DomainName is required.")
            .MaximumLength(253).WithMessage("DomainName cannot exceed 253 characters.")
            .Must(domain => !string.IsNullOrWhiteSpace(domain) && FqdnRegex.IsMatch(domain))
            .WithMessage("DomainName must be a valid FQDN (e.g., 'mail.aurora.vn').");

        RuleFor(x => x.MaxMailboxCount)
            .InclusiveBetween(1, 10000).WithMessage("MaxMailboxCount must be between 1 and 10,000.");

        RuleFor(x => x.RetentionDays)
            .InclusiveBetween(1, 3650).WithMessage("RetentionDays must be between 1 and 3,650 days.");
    }
}

public class CreateMailboxRequestValidator : AbstractValidator<CreateMailboxRequest>
{
    private static readonly Regex LocalPartRegex = new(@"^[A-Za-z0-9._%+-]+$", RegexOptions.Compiled);

    public CreateMailboxRequestValidator()
    {
        RuleFor(x => x.DomainId)
            .NotEmpty().WithMessage("DomainId is required.")
            .Must(id => Guid.TryParse(id, out _)).WithMessage("DomainId must be a valid GUID.");

        RuleFor(x => x.LocalPart)
            .NotEmpty().WithMessage("LocalPart is required.")
            .MaximumLength(64).WithMessage("LocalPart cannot exceed 64 characters.")
            .Must(lp => !string.IsNullOrWhiteSpace(lp) && LocalPartRegex.IsMatch(lp))
            .WithMessage("LocalPart contains invalid characters.");

        When(x => !string.IsNullOrEmpty(x.UserId), () =>
        {
            RuleFor(x => x.UserId)
                .Must(id => Guid.TryParse(id, out _)).WithMessage("UserId must be a valid GUID.");
        });
    }
}

public class CreateAliasRequestValidator : AbstractValidator<CreateAliasRequest>
{
    public CreateAliasRequestValidator()
    {
        RuleFor(x => x.DomainId)
            .NotEmpty().WithMessage("DomainId is required.")
            .Must(id => Guid.TryParse(id, out _)).WithMessage("DomainId must be a valid GUID.");

        RuleFor(x => x.AliasAddress)
            .NotEmpty().WithMessage("AliasAddress is required.")
            .EmailAddress().WithMessage("AliasAddress must be a valid email address.");

        RuleFor(x => x.TargetAddresses)
            .NotEmpty().WithMessage("TargetAddresses list cannot be empty.")
            .Must(targets => targets.Count <= 20).WithMessage("Cannot specify more than 20 target addresses per alias.");

        RuleForEach(x => x.TargetAddresses)
            .NotEmpty().WithMessage("Target email address cannot be empty.")
            .EmailAddress().WithMessage("Target address must be a valid email address.");
    }
}

public class CreateDraftRequestValidator : AbstractValidator<CreateDraftRequest>
{
    public CreateDraftRequestValidator()
    {
        RuleFor(x => x.MailboxId)
            .NotEmpty().WithMessage("MailboxId is required.")
            .Must(id => Guid.TryParse(id, out _)).WithMessage("MailboxId must be a valid GUID.");

        RuleFor(x => x.Subject)
            .NotNull().WithMessage("Subject cannot be null.")
            .MaximumLength(500).WithMessage("Subject cannot exceed 500 characters.");

        RuleFor(x => x.Body)
            .NotNull().WithMessage("Body cannot be null.")
            .MaximumLength(1048576).WithMessage("Body cannot exceed 1MB.");

        When(x => !string.IsNullOrEmpty(x.AssignedStaffId), () =>
        {
            RuleFor(x => x.AssignedStaffId)
                .Must(id => Guid.TryParse(id, out _)).WithMessage("AssignedStaffId must be a valid GUID.");
        });
    }
}

public class SubmitOutboundMessageRequestValidator : AbstractValidator<SubmitOutboundMessageRequest>
{
    public SubmitOutboundMessageRequestValidator()
    {
        RuleFor(x => x.SenderAddress)
            .NotEmpty().WithMessage("SenderAddress is required.")
            .EmailAddress().WithMessage("SenderAddress must be a valid email address.");

        RuleFor(x => x.RecipientAddresses)
            .NotEmpty().WithMessage("RecipientAddresses list cannot be empty.")
            .Must(r => r.Count <= MailLimits.MaxRecipientCount)
            .WithMessage($"Cannot exceed {MailLimits.MaxRecipientCount} recipients per outbound message.");

        RuleForEach(x => x.RecipientAddresses)
            .NotEmpty().WithMessage("Recipient address cannot be empty.")
            .EmailAddress().WithMessage("Recipient must be a valid email address.");

        RuleFor(x => x.Subject)
            .NotNull().WithMessage("Subject cannot be null.")
            .MaximumLength(MailLimits.MaxSubjectLength)
            .WithMessage($"Subject cannot exceed {MailLimits.MaxSubjectLength} characters.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.BodyText) || !string.IsNullOrWhiteSpace(x.BodyHtml))
            .WithMessage("Either BodyText or BodyHtml must be provided.");

        When(x => x.Attachments != null && x.Attachments.Count > 0, () =>
        {
            RuleFor(x => x.Attachments)
                .Must(att => att!.Count <= MailLimits.MaxAttachmentCount)
                .WithMessage($"Cannot attach more than {MailLimits.MaxAttachmentCount} files per message.");

            RuleFor(x => x.Attachments)
                .Must(ValidateTotalDecodedSize)
                .WithMessage($"Total decoded attachment size cannot exceed {MailLimits.MaxTotalAttachmentBytes / (1024 * 1024)} MB.");

            RuleForEach(x => x.Attachments).ChildRules(att =>
            {
                att.RuleFor(a => a.Filename)
                    .NotEmpty().WithMessage("Attachment filename is required.")
                    .MaximumLength(255).WithMessage("Attachment filename cannot exceed 255 characters.");

                att.RuleFor(a => a.ContentType)
                    .NotEmpty().WithMessage("Attachment ContentType is required.");

                att.RuleFor(a => a.ContentBase64)
                    .NotEmpty().WithMessage("Attachment ContentBase64 is required.")
                    .Must(BeValidBase64AndUnderSingleLimit)
                    .WithMessage($"Attachment must be valid base64 and under {MailLimits.MaxSingleAttachmentBytes / (1024 * 1024)} MB.");
            });
        });
    }

    private static bool BeValidBase64AndUnderSingleLimit(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return false;
        if (EstimateDecodedLength(base64) > MailLimits.MaxSingleAttachmentBytes) return false;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return bytes.Length <= MailLimits.MaxSingleAttachmentBytes;
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateTotalDecodedSize(List<OutboundAttachmentDto>? attachments)
    {
        if (attachments == null || attachments.Count == 0) return true;

        long totalEstimated = 0;
        foreach (var att in attachments)
        {
            if (string.IsNullOrWhiteSpace(att.ContentBase64)) return false;
            totalEstimated += EstimateDecodedLength(att.ContentBase64);
        }

        if (totalEstimated > MailLimits.MaxTotalAttachmentBytes * 1.05) return false;

        long totalDecoded = 0;
        foreach (var att in attachments)
        {
            try
            {
                var bytes = Convert.FromBase64String(att.ContentBase64);
                totalDecoded += bytes.Length;
                if (totalDecoded > MailLimits.MaxTotalAttachmentBytes) return false;
            }
            catch
            {
                return false;
            }
        }

        return totalDecoded <= MailLimits.MaxTotalAttachmentBytes;
    }

    private static long EstimateDecodedLength(string base64)
    {
        int padding = 0;
        if (base64.EndsWith("==")) padding = 2;
        else if (base64.EndsWith("=")) padding = 1;
        return (long)(base64.Length * 0.75) - padding;
    }
}
