using System;
using System.Collections.Generic;

namespace IamTenant.Application.DTOs.Roles;

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UserPermissionsDto
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<PermissionDto> Permissions { get; set; } = [];
    public int Version { get; set; }
    public bool FromCache { get; set; }
}
