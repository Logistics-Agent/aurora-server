using System.Text.Json;

namespace RegulatoryCompliance.Domain.Entities;

internal static class ComplianceValidation
{
    public static Guid RequiredId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        return value;
    }

    public static string RequiredText(string? value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} exceeds {maxLength} characters.");
        return normalized;
    }

    public static string? OptionalText(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return RequiredText(value, parameterName, maxLength);
    }

    public static decimal Confidence(decimal value, string parameterName)
    {
        if (value is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(parameterName, "Confidence must be between 0 and 1.");
        return value;
    }

    public static double Score(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(parameterName, "Score must be between 0 and 1.");
        return value;
    }

    public static string Json(string value, string parameterName, int maxLength)
    {
        var normalized = RequiredText(value, parameterName, maxLength);
        try
        {
            using var _ = JsonDocument.Parse(normalized);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"{parameterName} must contain valid JSON.", parameterName, exception);
        }
        return normalized;
    }

    public static string Sha256(string value, string parameterName)
    {
        var normalized = RequiredText(value, parameterName, 64).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException($"{parameterName} must be a lowercase SHA-256 hex digest.", parameterName);
        return normalized;
    }

    public static DateTimeOffset RequiredTimestamp(DateTimeOffset value, string parameterName)
    {
        if (value == default)
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        return value;
    }
}
