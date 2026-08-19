using System.Text.RegularExpressions;

namespace MailService.Domain.ValueObjects;

public record EmailAddress
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value {; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !EmailRegex.IsMatch(value))
        {
            throw new ArgumentException($"Invalid RFC 5321 email address format: '{value}'", nameof(value));
        }
        Value = value.Trim().ToLowerInvariant();
    }

    public static EmailAddress Create(string value) => new(value);
    public override string ToString() => Value;
}

public record DomainName
{
    private static readonly Regex DomainRegex = new(
        @"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9][a-z0-9-]{0,61}[a-z0-9]$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value {; }

    public DomainName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !DomainRegex.IsMatch(value))
        {
            throw new ArgumentException($"Invalid domain name format: '{value}'", nameof(value));
        }
        Value = value.Trim().ToLowerInvariant();
    }

    public static DomainName Create(string value) => new(value);
    public override string ToString() => Value;
}

public record SpamScore(decimal Value)
{
    public static SpamScore Zero => new(0.0m);
}

public record PhishingScore
{
    public decimal Value {; }

    public PhishingScore(decimal value)
    {
        if (value < 0.0m || value > 1.0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Phishing score must be between 0.0 and 1.0.");
        }
        Value = value;
    }

    public static PhishingScore Zero => new(0.0m);
}

public record PipelineExecutionId(Guid Value)
{
    public static PipelineExecutionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
