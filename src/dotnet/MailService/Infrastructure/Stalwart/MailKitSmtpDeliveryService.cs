using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Interfaces.Transport;
using MailService.Application.Options;
using MailService.Infrastructure.Transport;

namespace MailService.Infrastructure.Stalwart;

public class MailKitSmtpDeliveryService : ISmtpDeliveryService
{
    private readonly IMailTransport _activeTransport;
    private readonly ILogger<MailKitSmtpDeliveryService> _logger;

    public MailKitSmtpDeliveryService(
        IConfiguration configuration,
        ILogger<MailKitSmtpDeliveryService> logger,
        IEnumerable<IMailTransport>? transports = null,
        IOptions<MailTransportOptions>? transportOptions = null,
        IOptions<BrevoOptions>? brevoOptions = null,
        IOptions<MailServiceOptions>? mailOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        _logger = logger;

        var configuredProvider = transportOptions?.Value?.Provider
            ?? configuration["MailTransport:Provider"]
            ?? configuration["Mail:OutboundProvider"]
            ?? "Brevo";

        var transportList = transports?.ToList();
        var selected = transportList?.FirstOrDefault(t => string.Equals(t.ProviderName, configuredProvider, StringComparison.OrdinalIgnoreCase));

        if (selected != null)
        {
            _activeTransport = selected;
        }
        else if (string.Equals(configuredProvider, "Stalwart", StringComparison.OrdinalIgnoreCase))
        {
            var stLogger = loggerFactory?.CreateLogger<StalwartMailTransport>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StalwartMailTransport>.Instance;
            _activeTransport = new StalwartMailTransport(configuration, mailOptions, stLogger);
        }
        else
        {
            var brLogger = loggerFactory?.CreateLogger<BrevoMailTransport>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BrevoMailTransport>.Instance;
            _activeTransport = new BrevoMailTransport(configuration, brevoOptions, brLogger);
        }

        _logger.LogInformation("MailKitSmtpDeliveryService initialized with active transport provider: {Provider}", _activeTransport.ProviderName);
    }

    public Task<SmtpDeliveryResult> DeliverAsync(
        string senderAddress,
        IReadOnlyList<string> recipientAddresses,
        string subject,
        string bodyText,
        string bodyHtml,
        IReadOnlyList<(string Filename, string ContentType, byte[] Content)> attachments,
        CancellationToken cancellationToken = default)
    {
        return _activeTransport.DeliverAsync(
            senderAddress,
            recipientAddresses,
            subject,
            bodyText,
            bodyHtml,
            attachments,
            cancellationToken);
    }
}
