namespace IamTenant.Domain;

public class UserRole
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public List<Guid> RoleIds { get; set; } = [];
    public Role? Role { get; set; }
}
