namespace MailService.Application.Options;

public class MailTransportOptions
{
    public const string SectionName = "MailTransport";

    public string Provider { get; set; } = "Brevo";
}
