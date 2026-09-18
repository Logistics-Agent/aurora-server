namespace MailService.Application.Options;

public class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string SmtpHost { get; set; } = "smtp-relay.brevo.com";
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string FromDomain { get; set; } = "e-verland.site";
}
