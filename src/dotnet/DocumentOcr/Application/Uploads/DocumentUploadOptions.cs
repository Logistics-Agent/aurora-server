namespace DocumentOcr.Application.Uploads;

public sealed class DocumentUploadOptions
{
    public const string SectionName = "DocumentUploads";

    public TimeSpan SessionExpiry { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromMinutes(1);

    public void Validate()
    {
        if (SessionExpiry != TimeSpan.FromMinutes(15))
            throw new InvalidOperationException("DocumentUploads:SessionExpiry must be exactly 00:15:00.");
        if (CleanupInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("DocumentUploads:CleanupInterval must be positive.");
    }
}
