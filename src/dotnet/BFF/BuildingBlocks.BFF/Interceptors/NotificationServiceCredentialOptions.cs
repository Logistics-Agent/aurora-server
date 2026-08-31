namespace BuildingBlocks.BFF.Interceptors;

public sealed class NotificationServiceCredentialOptions
{
    public const string SectionName = "Grpc:Notification";

    public string Url { get; init; } = string.Empty;
    public string ServiceApiKey { get; init; } = string.Empty;
}
