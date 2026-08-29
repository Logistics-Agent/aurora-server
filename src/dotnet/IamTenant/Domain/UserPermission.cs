using System;

namespace IamTenant.Domain;

public class UserPermission
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }

    public Guid TenantId { get; set; }
    public Guid? GrantedByUserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}
