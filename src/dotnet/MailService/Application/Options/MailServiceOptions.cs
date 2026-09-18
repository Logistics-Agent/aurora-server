using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace MailService.Application.Options;

public class MailServiceOptions
{
    public const string SectionName = "MailService";

    public string? DatabaseConnectionString { get; set; }
    public string? RedisConnectionString { get; set; }
    public string? RedisHost { get; set; }
    
    // RabbitMQ Options
    public string? RabbitMqHost { get; set; }
    public int RabbitMqPort { get; set; } = 5672;
    public string? RabbitMqUsername { get; set; }
    public string? RabbitMqPassword { get; set; }
    public string? RabbitMqVirtualHost { get; set; } = "mail";

    // Stalwart Options
    public string? StalwartBaseUrl { get; set; }
    public string? StalwartAdminUrl { get; set; }
    public string? StalwartAdminApiKey { get; set; }
    public string? StalwartWebhookSecret { get; set; }
    public string? StalwartSmtpHost { get; set; }
    public int StalwartSmtpPort { get; set; } = 25;
    public string? StalwartSmtpUser { get; set; }
    public string? StalwartSmtpPassword { get; set; }

    // Transport & Provider Options (Brevo & Cloudflare)
    public string MailTransportProvider { get; set; } = "Brevo";
    public string? BrevoSmtpHost { get; set; } = "smtp-relay.brevo.com";
    public int BrevoSmtpPort { get; set; } = 587;
    public string? BrevoSmtpUsername { get; set; }
    public string? BrevoSmtpPassword { get; set; }
    public string? CloudflareWebhookSecret { get; set; }

    public string? ClamAvHost { get; set; }
    public int ClamAvPort { get; set; } = 3310;
    public bool ClamAvEnabled { get; set; } = true;
    public bool ClamAvFailOpen { get; set; } = false;

    public string? SpamAssassinHost { get; set; }
    public int SpamAssassinPort { get; set; } = 783;

    public string? AiGovernanceEndpoint { get; set; }
}

public class MailServiceOptionsValidator : IValidateOptions<MailServiceOptions>
{
    private readonly bool _isProduction;

    public MailServiceOptionsValidator(bool isProduction = false)
    {
        _isProduction = isProduction;
    }

    public ValidateOptionsResult Validate(string? name, MailServiceOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DatabaseConnectionString))
        {
            failures.Add("ConnectionStrings:DefaultConnection is required.");
        }

        if (string.IsNullOrWhiteSpace(options.RedisConnectionString) && string.IsNullOrWhiteSpace(options.RedisHost))
        {
            failures.Add("Redis:ConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(options.RabbitMqHost))
        {
            failures.Add("RabbitMQ:Host is required.");
        }

        if (options.RabbitMqPort <= 0 || options.RabbitMqPort > 65535)
        {
            failures.Add("RabbitMQ:Port must be between 1 and 65535.");
        }

        if (!string.IsNullOrWhiteSpace(options.StalwartBaseUrl) &&
            !Uri.TryCreate(options.StalwartBaseUrl, UriKind.Absolute, out _))
        {
            failures.Add("Stalwart:BaseUrl must be a valid absolute URI.");
        }

        if (options.StalwartSmtpPort <= 0 || options.StalwartSmtpPort > 65535)
        {
            failures.Add("Stalwart:SmtpPort must be between 1 and 65535.");
        }

        if (!string.IsNullOrWhiteSpace(options.AiGovernanceEndpoint) &&
            !Uri.TryCreate(options.AiGovernanceEndpoint, UriKind.Absolute, out _))
        {
            failures.Add("AiGovernance:GrpcEndpoint must be a valid absolute URI.");
        }

        if (options.ClamAvPort <= 0 || options.ClamAvPort > 65535)
        {
            failures.Add("ClamAV:Port must be between 1 and 65535.");
        }

        if (options.SpamAssassinPort <= 0 || options.SpamAssassinPort > 65535)
        {
            failures.Add("SpamAssassin:Port must be between 1 and 65535.");
        }

        if (_isProduction)
        {
            if (string.IsNullOrWhiteSpace(options.RabbitMqPassword))
            {
                failures.Add("RabbitMQ:Password is required in Production.");
            }

            if (string.Equals(options.MailTransportProvider, "Stalwart", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(options.StalwartBaseUrl))
            {
                failures.Add("Stalwart:BaseUrl is required in Production when using Stalwart provider.");
            }

            if (string.IsNullOrWhiteSpace(options.AiGovernanceEndpoint))
            {
                failures.Add("AiGovernance:GrpcEndpoint is required in Production.");
            }

            if (options.DatabaseConnectionString != null &&
                (options.DatabaseConnectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                 options.DatabaseConnectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
            {
                failures.Add("Production database connection string must point to external managed database (Neon), not localhost.");
            }
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
