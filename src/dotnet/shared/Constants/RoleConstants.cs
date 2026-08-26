using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Security;

namespace Shared.Constants;

public static class RoleConstants
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string TenantAdmin = "TENANT_ADMIN";
    public const string Staff = "STAFF";
    public const string Manager = "MANAGER";

    public static readonly IReadOnlyList<string> All = [SystemAdmin, TenantAdmin, Staff, Manager];
}

public static class CurrentUserServiceExtensions
{
    public static bool IsSystemAdmin(this ICurrentUserService user) =>
        user != null && user.RoleIds != null && user.RoleIds.Any(r => string.Equals(r, RoleConstants.SystemAdmin, StringComparison.OrdinalIgnoreCase));

    public static bool IsTenantAdmin(this ICurrentUserService user) =>
        user != null && user.RoleIds != null && user.RoleIds.Any(r => string.Equals(r, RoleConstants.TenantAdmin, StringComparison.OrdinalIgnoreCase));

    public static bool IsManager(this ICurrentUserService user) =>
        user != null && user.RoleIds != null && user.RoleIds.Any(r => string.Equals(r, RoleConstants.Manager, StringComparison.OrdinalIgnoreCase));

    public static bool CanPublishRiskPolicy(this ICurrentUserService user) =>
        user != null && (
            user.IsSystemAdmin() ||
            user.IsTenantAdmin() ||
            user.IsManager() ||
            (user.Permissions != null && user.Permissions.Any(p =>
                string.Equals(p, "route_planning:release", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "route_planning:publish", StringComparison.OrdinalIgnoreCase)))
        );
}
