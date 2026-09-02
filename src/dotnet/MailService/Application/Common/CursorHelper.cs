using System;
using System.Globalization;
using System.Text;

namespace MailService.Application.Common;

/// <summary>
/// Helper for Base64 Keyset Cursor pagination (DateTimeOffset + Guid).
/// </summary>
public static class CursorHelper
{
    public static string Encode(DateTimeOffset timestamp, Guid id)
    {
        string raw = $"{timestamp.ToString("O", CultureInfo.InvariantCulture)}|{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? token, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = default;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(token);
            string raw = Encoding.UTF8.GetString(bytes);
            string[] parts = raw.Split('|');

            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp))
            {
                return false;
            }

            return Guid.TryParseExact(parts[1], "N", out id) || Guid.TryParse(parts[1], out id);
        }
        catch
        {
            return false;
        }
    }
}
