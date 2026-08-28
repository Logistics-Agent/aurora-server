using Shared.Entity;
using Shared.Enums;

namespace IamTenant.Domain;

public class User : TenantAuditableEntity
{
    public string? CognitoSub { get; set; }
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Optional properties for specific staff/users
    public string? StaffCode { get; set; }
    public string? Department { get; set; }

    /// <summary>
    /// Persona / UI presentation role (SYSTEM_ADMIN, TENANT_ADMIN, MANAGER, STAFF).
    /// Authority comes strictly from UserPermissions.
    /// </summary>
    public BaseRole Role { get; set; } = BaseRole.Staff;
    public Enums.UserStatus Status { get; set; } = Enums.UserStatus.Invited;

    public int PermissionVersion { get; set; } = 1;

    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted { get; private set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public Tenant? Tenant { get; set; }
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
}
