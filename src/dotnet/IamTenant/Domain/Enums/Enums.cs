namespace IamTenant.Domain.Enums;

public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
    Archived
}

public enum UserStatus
{
    Invited,
    Active,
    Blocked
}

public enum PlanType
{
    Standard,
    Enterprise
}
