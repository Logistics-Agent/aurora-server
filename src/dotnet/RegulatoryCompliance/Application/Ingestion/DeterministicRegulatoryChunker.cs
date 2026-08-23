using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed partial class DeterministicRegulatoryChunker : IRegulatoryChunker
{
    public const int MaxChunkCharacters = 1_200;

    public IReadOnlyList<RegulatoryChunkDraft> Chunk(string content)
    {
        var normalized = Normalize(content);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Regulatory content is empty.", nameof(content));

        var drafts = new List<RegulatoryChunkDraft>();
        string? section = null;
        string? page = null;
        var cursor = 0;
        foreach (var line in normalized.Split('\n'))
        {
            var lineStart = cursor;
            cursor += line.Length + 1;
            if (line.StartsWith("[[PAGE:", StringComparison.Ordinal) && line.EndsWith("]]", StringComparison.Ordinal))
            {
                page = line[7..^2].Trim();
                continue;
            }
            if (line.StartsWith('#'))
                section = line.TrimStart('#', ' ');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (var part in SplitLine(line, MaxChunkCharacters))
            {
                var offset = line.IndexOf(part, StringComparison.Ordinal);
                var start = lineStart + Math.Max(offset, 0);
                drafts.Add(new RegulatoryChunkDraft(
                    drafts.Count + 1,
                    section,
                    page,
                    part,
                    CountTokens(part),
                    start,
                    start + part.Length,
                    Sha256(part)));
            }
        }
        return drafts;
    }

    public static string Normalize(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => HorizontalWhitespace().Replace(line.Trim(), " "));
        return string.Join('\n', lines).Trim();
    }

    private static IEnumerable<string> SplitLine(string line, int maxLength)
    {
        for (var offset = 0; offset < line.Length;)
        {
            var length = Math.Min(maxLength, line.Length - offset);
            if (offset + length < line.Length)
            {
                var boundary = line.LastIndexOf(' ', offset + length - 1, length);
                if (boundary > offset)
                    length = boundary - offset;
            }
            var part = line.Substring(offset, length).Trim();
            if (part.Length > 0)
                yield return part;
            offset += length;
            while (offset < line.Length && line[offset] == ' ')
                offset++;
        }
    }

    private static int CountTokens(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [GeneratedRegex("[\\t ]+")]
    private static partial Regex HorizontalWhitespace();
}
