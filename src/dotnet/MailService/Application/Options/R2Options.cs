namespace MailService.Application.Options;

public class R2Options
{
    public const string SectionName = "R2";

    public string? AccountId { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string BucketName { get; set; } = "aurora-mail-platform";
    public string? ServiceUrl { get; set; }
}
