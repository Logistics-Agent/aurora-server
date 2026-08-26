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

    public static readonly IReadOnlyList<string> All = [SystemAdmin, TenantAdmin, Staff];
}

public static class CurrentUserServiceExtensions
{
    public static bool IsSystemAdmin(this ICurrentUserService user) =>
        user != null && user.RoleIds.Any(r => string.Equals(r, RoleConstants.SystemAdmin, StringComparison.OrdinalIgnoreCase));

    public static bool IsTenantAdmin(this ICurrentUserService user) =>
        user != null && user.RoleIds.Any(r => string.Equals(r, RoleConstants.TenantAdmin, StringComparison.OrdinalIgnoreCase));
}
