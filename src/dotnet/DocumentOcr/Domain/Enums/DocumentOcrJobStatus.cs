namespace DocumentOcr.Domain.Enums;

public enum DocumentOcrJobStatus
{
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Rejected = 6
}
