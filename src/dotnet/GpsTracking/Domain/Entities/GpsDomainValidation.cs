namespace GpsTracking.Domain.Entities;

internal static class GpsDomainValidation
{
    internal static Guid RequiredId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{parameterName} is required.", parameterName);
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

    internal static decimal Latitude(decimal value, string parameterName = "latitude")
    {
        if (value is < -90 or > 90)
            throw new ArgumentOutOfRangeException(parameterName, "Latitude must be between -90 and 90.");
        return value;
    }

    internal static decimal Longitude(decimal value, string parameterName = "longitude")
    {
        if (value is < -180 or > 180)
            throw new ArgumentOutOfRangeException(parameterName, "Longitude must be between -180 and 180.");
        return value;
    }

    internal static decimal? NonNegative(decimal? value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} cannot be negative.");
        return value;
    }
}
