using MailService.Domain.Enums;

namespace MailService.Application.Interfaces.Classification;

public interface IEmailClassifier
{
    Task<EmailCategory> ClassifyAsync(string subject, string body, CancellationToken cancellationToken = default);
}
