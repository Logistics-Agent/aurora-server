namespace MailService.Application.Interfaces.Security;

public interface ISpamAssassinClient
{
    Task<(decimal Score, List<string> Rules)> CheckSpamAsync(byte[] emlBytes, CancellationToken cancellationToken = default);
}
