namespace MailService.Application.Interfaces.Security;

public interface IClamAvClient
{
    Task<(bool IsClean, string VirusName)> ScanStreamAsync(Stream stream, CancellationToken cancellationToken = default);
}
