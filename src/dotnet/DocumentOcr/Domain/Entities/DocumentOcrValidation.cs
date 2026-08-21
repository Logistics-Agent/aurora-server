using System.Text.Json;

namespace DocumentOcr.Domain.Entities;

internal static class DocumentOcrValidation
{
    internal static Guid RequiredId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        return value;
    }

    internal static Guid? OptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        return value;
    }

    internal static string RequiredText(string? value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} cannot exceed {maxLength} characters.");
        return normalized;
    }

    internal static string? OptionalText(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return RequiredText(value, parameterName, maxLength);
    }

    internal static decimal Confidence(decimal value, string parameterName)
    {
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be between 0 and 1.");
        return value;
    }

    internal static string Json(string value, string parameterName, int maxLength)
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

    internal static string? OptionalJson(string? value, string parameterName, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Json(value, parameterName, maxLength);
    }
}
