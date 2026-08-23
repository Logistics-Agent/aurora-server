namespace DocumentOcr.Domain.Enums;

public enum OcrAttemptOutcome
{
    Processing = 1,
    Succeeded = 2,
    TransientFailure = 3,
    PermanentFailure = 4,
    InvalidDocument = 5,
    UnsupportedFormat = 6,
    Cancelled = 7
}
