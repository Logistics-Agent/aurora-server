using System.Net.Mail;
using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

internal static class DomainValidation
{
    internal static void RequiredId(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(name + " is required.", name);
    }

    internal static string RequiredText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(name + " is required.", name);

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException(name + " is too long.", name);
        return normalized;
    }

    internal static string? Recipient(NotificationChannel channel, string? value)
    {
        if (channel == NotificationChannel.InApp)
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        var normalized = RequiredText(value, nameof(value), 320);
        if (!MailAddress.TryCreate(normalized, out _))
            throw new ArgumentException("Recipient must be a valid email.", nameof(value));
        return normalized;
    }
}
