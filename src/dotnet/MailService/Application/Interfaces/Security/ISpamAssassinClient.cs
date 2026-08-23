using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MailService.Application.Interfaces.Security;

public enum SpamAssassinStatus
{
    Scored,
    ServiceUnavailable,
    ScanError
}

public record SpamAssassinScanResult(
    SpamAssassinStatus Status,
    decimal Score,
    decimal Threshold = 5.0m,
    IReadOnlyList<string>? MatchedRules = null,
    string? ErrorMessage = null,
    int DurationMs = 0
)
{
    public bool IsSpam => Score >= Threshold;
    public bool IsReject => Score >= 10.0m;

    public static SpamAssassinScanResult ScoredResult(decimal score, decimal threshold, IReadOnlyList<string> rules, int durationMs) =>
        new(SpamAssassinStatus.Scored, score, threshold, rules, DurationMs: durationMs);

    public static SpamAssassinScanResult Unavailable(string error) =>
        new(SpamAssassinStatus.ServiceUnavailable, 0.0m, ErrorMessage: error);

    public static SpamAssassinScanResult Error(string error) =>
        new(SpamAssassinStatus.ScanError, 0.0m, ErrorMessage: error);
}

public interface ISpamAssassinClient
{
    Task<SpamAssassinScanResult> CheckSpamAsync(byte[] emlBytes, CancellationToken cancellationToken = default);
}
