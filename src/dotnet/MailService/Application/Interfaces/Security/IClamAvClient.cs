using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MailService.Application.Interfaces.Security;

public enum ClamAvStatus
{
    Clean,
    Infected,
    ServiceUnavailable,
    ScanError
}

public record ClamAvScanResult(
    ClamAvStatus Status,
    string? VirusName = null,
    string? ErrorMessage = null,
    long ScannedBytes = 0,
    int DurationMs = 0
)
{
    public bool IsClean => Status == ClamAvStatus.Clean;
    public bool IsInfected => Status == ClamAvStatus.Infected;

    public static ClamAvScanResult CleanResult(long bytes, int durationMs) =>
        new(ClamAvStatus.Clean, ScannedBytes: bytes, DurationMs: durationMs);

    public static ClamAvScanResult InfectedResult(string virusName, long bytes, int durationMs) =>
        new(ClamAvStatus.Infected, VirusName: virusName, ScannedBytes: bytes, DurationMs: durationMs);

    public static ClamAvScanResult Unavailable(string error) =>
        new(ClamAvStatus.ServiceUnavailable, ErrorMessage: error);

    public static ClamAvScanResult Error(string error) =>
        new(ClamAvStatus.ScanError, ErrorMessage: error);
}

public interface IClamAvClient
{
    Task<ClamAvScanResult> ScanStreamAsync(Stream stream, CancellationToken cancellationToken = default);
}
