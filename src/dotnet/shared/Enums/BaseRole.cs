using System;

namespace Shared.Enums;

/// <summary>
/// Canonical Base Role in Aurora representing persona, layout, and system vs tenant scope.
/// Role determines persona; Permission determines authority.
/// </summary>
public enum BaseRole
{
    Staff = 1,
    Manager = 2,
    TenantAdmin = 3,
    SystemAdmin = 4
}

public static class BaseRoleExtensions
{
    public const string SystemAdminCode = "SYSTEM_ADMIN";
    public const string TenantAdminCode = "TENANT_ADMIN";
    public const string ManagerCode = "MANAGER";
    public const string StaffCode = "STAFF";

    public static string ToCode(this BaseRole role) => role switch
    {
        BaseRole.SystemAdmin => SystemAdminCode,
        BaseRole.TenantAdmin => TenantAdminCode,
        BaseRole.Manager => ManagerCode,
        BaseRole.Staff => StaffCode,
        _ => StaffCode
    };

    public static BaseRole ParseRole(string? roleStr)
    {
        if (string.IsNullOrWhiteSpace(roleStr))
            return BaseRole.Staff;

        var normalized = roleStr.Trim().ToUpperInvariant();
        return normalized switch
        {
            "SYSTEM_ADMIN" or "SYSTEMADMIN" => BaseRole.SystemAdmin,
            "TENANT_ADMIN" or "TENANTADMIN" => BaseRole.TenantAdmin,
            "MANAGER" or "TENANTMANAGER" => BaseRole.Manager,
            "STAFF" or "TENANTSTAFF" => BaseRole.Staff,
            _ => throw new ArgumentException($"Invalid role '{roleStr}'. Valid roles: SYSTEM_ADMIN, TENANT_ADMIN, MANAGER, STAFF.")
        };
    }

    public static bool TryParseRole(string? roleStr, out BaseRole role)
    {
        try
        {
            role = ParseRole(roleStr);
            return true;
        }
        catch
        {
            role = BaseRole.Staff;
            return false;
        }
    }

    public static bool IsTenantAssignable(this BaseRole role) =>
        role is BaseRole.Staff or BaseRole.Manager or BaseRole.TenantAdmin;
}
