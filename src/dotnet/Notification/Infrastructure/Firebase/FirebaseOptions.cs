namespace Notification.Infrastructure.Firebase;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";
    public string ProjectId { get; init; } = string.Empty;
    public string ClientEmail { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
    public string CredentialsPath { get; init; } = string.Empty;
    public bool Enabled { get; init; }

    public bool HasInlineCredentials =>
        !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(ClientEmail)
        && !string.IsNullOrWhiteSpace(PrivateKey);
}
