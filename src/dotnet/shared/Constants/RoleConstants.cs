using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Security;

namespace Shared.Constants;

public static class RoleConstants
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string TenantAdmin = "TENANT_ADMIN";
    public const string Manager = "MANAGER";
    public const string Staff = "STAFF";

    public static readonly IReadOnlyList<string> All = [SystemAdmin, TenantAdmin, Manager, Staff];
    public static readonly IReadOnlyList<string> TenantAssignable = [TenantAdmin, Manager, Staff];
}

public static class CurrentUserServiceExtensions
{
    public static bool IsSystemAdmin(this ICurrentUserService user) =>
        user != null && string.Equals(user.Role, RoleConstants.SystemAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool IsTenantAdmin(this ICurrentUserService user) =>
        user != null && string.Equals(user.Role, RoleConstants.TenantAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool IsManager(this ICurrentUserService user) =>
        user != null && string.Equals(user.Role, RoleConstants.Manager, StringComparison.OrdinalIgnoreCase);
}
