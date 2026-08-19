namespace MailService.Domain.Enums;

public enum EmailDirection
{
    Inbound = 0,
    Outbound = 1
}

public enum PipelineStatus
{
    Pending = 0,
    Running = 1,
    Delivered = 2,
    Quarantined = 3,
    Failed = 4,
    DeadLettered = 5
}

public enum QuarantineStatus
{
    Pending = 0,
    Released = 1,
    Deleted = 2
}

public enum EmailCategory
{
    BookingRequest = 0,
    ShipmentUpdate = 1,
    Quotation = 2,
    Complaint = 3,
    Spam = 4,
    Unknown = 5
}

public enum SecurityCheckStage
{
    TlsVerification = 0,
    HeaderParsing = 1,
    RecipientValidation = 2,
    SpfValidation = 3,
    DkimValidation = 4,
    DmarcEvaluation = 5,
    TenantValidation = 6,
    AttachmentValidation = 7,
    SpamScoring = 8,
    AiPhishingDetection = 9,
    HeaderForgeryAnalysis = 10,
    Classification = 11,
    OutboundAttachmentValidation = 12,
    PolicyValidation = 13,
    AiRiskScoring = 14,
    RateLimitCheck = 15,
    AuditCreation = 16,
    StalwartSmtpSubmission = 17
}

public enum DraftSource
{
    Manual = 0,
    AiAgent = 1
}

public enum DraftStatus
{
    Draft = 0,
    Sent = 1,
    Discarded = 2
}

public enum DomainStatus
{
    Active = 0,
    Suspended = 1
}

public enum MailboxStatus
{
    Active = 0,
    Suspended = 1,
    Deleted = 2
}

public enum ActorType
{
    System = 0,
    TenantAdmin = 1,
    Staff = 2,
    Service = 3
}
