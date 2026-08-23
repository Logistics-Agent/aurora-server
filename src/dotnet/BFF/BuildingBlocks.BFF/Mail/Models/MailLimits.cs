namespace BuildingBlocks.BFF.Mail.Models;

/// <summary>
/// Authoritative payload limits and constraints for Mail API & BFF integration.
/// Standardized relationship: MaxTotalAttachmentBytes (50MB) < MaxGrpcMessageBytes (75MB) < MaxHttpRequestBodyBytes (80MB).
/// </summary>
public static class MailLimits
{
    /// <summary>Số lượng tệp đính kèm tối đa trên một email.</summary>
    public const int MaxAttachmentCount = 10;

    /// <summary>Dung lượng giải mã tối đa của một tệp đính kèm đơn lẻ (25 MB).</summary>
    public const long MaxSingleAttachmentBytes = 25 * 1024 * 1024;

    /// <summary>Tổng dung lượng giải mã tối đa của tất cả tệp đính kèm trong một email (50 MB).</summary>
    public const long MaxTotalAttachmentBytes = 50 * 1024 * 1024;

    /// <summary>Số lượng người nhận tối đa trên một email outbound.</summary>
    public const int MaxRecipientCount = 50;

    /// <summary>Độ dài tối đa của tiêu đề email.</summary>
    public const int MaxSubjectLength = 500;

    /// <summary>Độ dài tối đa của nội dung văn bản email (1 MB).</summary>
    public const int MaxBodyLength = 1024 * 1024;

    /// <summary>Giới hạn dung lượng HTTP Request Body tại BFF Gateway/Kestrel (80 MB).</summary>
    public const int MaxHttpRequestBodyBytes = 80 * 1024 * 1024;

    /// <summary>Giới hạn dung lượng gRPC message gửi và nhận giữa BFF và MailService (75 MB).</summary>
    public const int MaxGrpcMessageBytes = 75 * 1024 * 1024;
}
