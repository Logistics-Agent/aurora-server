namespace MailService.Application.Options;

public class CloudflareInboundOptions
{
    public const string SectionName = "CloudflareInbound";

    public string? WebhookSecret { get; set; }
}
