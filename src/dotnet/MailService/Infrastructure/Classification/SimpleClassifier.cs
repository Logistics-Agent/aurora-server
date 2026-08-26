using MailService.Application.Interfaces.Classification;
using MailService.Domain.Enums;

namespace MailService.Infrastructure.Classification;

public class SimpleClassifier : IEmailClassifier
{
    public Task<EmailCategory> ClassifyAsync(string subject, string body, CancellationToken cancellationToken = default)
    {
        string text = $"{subject} {body}".ToLowerInvariant();
        if (text.Contains("booking")) return Task.FromResult(EmailCategory.BookingRequest);
        if (text.Contains("shipment") || text.Contains("tracking")) return Task.FromResult(EmailCategory.ShipmentUpdate);
        if (text.Contains("quote") || text.Contains("price")) return Task.FromResult(EmailCategory.Quotation);
        if (text.Contains("complaint")) return Task.FromResult(EmailCategory.Complaint);
        return Task.FromResult(EmailCategory.Unknown);
    }
}
