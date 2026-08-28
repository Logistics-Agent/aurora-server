using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Security;

/// <summary>
/// Cung cấp thông tin về người dùng hiện tại, được populate từ JWT bởi CurrentUserContextMiddleware
/// và enriched với direct permissions từ Redis bởi PermissionVersionMiddleware.
/// Được đăng ký Scoped để mỗi request có context riêng biệt.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId => null;
    Guid? TenantId => null;
    string? TraceId => null;
    int? PermissionVersion => null;
    string? Role => null;
    IReadOnlyList<string> Permissions => Array.Empty<string>();
}

public static class CurrentUserServiceExtensions
{
    public static bool HasPermission(this ICurrentUserService? user, string permission)
    {
        if (user == null || user.Permissions == null || string.IsNullOrWhiteSpace(permission))
            return false;

        return user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}

public interface ICurrentUserContext : ICurrentUserService
{
    /// <summary>
    /// Populate identity fields từ validated ClaimsPrincipal (JWT claims).
    /// </summary>
    void Populate(Guid? userId, Guid? tenantId, string? traceId, int? permissionVersion,
                  string? role, List<string> permissions);

    /// <summary>
    /// Populate permissions và role từ Redis cache (sau khi version đã được xác nhận).
    /// </summary>
    void PopulatePermissions(List<string> permissions, string? role);
}

public class CurrentUserService : ICurrentUserContext
{
    public Guid?   UserId            { get; private set; }
    public Guid?   TenantId          { get; private set; }
    public string? TraceId           { get; private set; }
    public int?    PermissionVersion { get; private set; }
    public string? Role              { get; private set; }
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    public void Populate(Guid? userId, Guid? tenantId, string? traceId, int? permissionVersion,
                         string? role, List<string> permissions)
    {
        UserId            = userId;
        TenantId          = tenantId;
        TraceId           = traceId;
        PermissionVersion = permissionVersion;
        Role              = role;
        Permissions       = permissions;
    }

    public void PopulatePermissions(List<string> permissions, string? role)
    {
        Permissions = permissions;
        if (!string.IsNullOrEmpty(role))
        {
            Role = role;
        }
    }
}
