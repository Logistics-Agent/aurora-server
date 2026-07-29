namespace IamTenant.Domain;

/// <summary>
/// Junction table User ↔ Role. Mỗi row = 1 user gán 1 role
/// (trước đây RoleIds là List&lt;Guid&gt; — không thể làm composite key và navigation Role không hoạt động).
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}
