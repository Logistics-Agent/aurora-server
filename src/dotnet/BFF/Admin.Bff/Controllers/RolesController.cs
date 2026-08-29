using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Enums;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Roles catalog (Canonical Base Roles & Default Template Permissions).
/// Route: /api/v1/admin/roles
/// </summary>
[ApiVersion("1.0")]
public class RolesController(
    ICurrentUserService currentUser,
    ILogger<RolesController> logger) : AdminControllerBase
{
    private static readonly List<RoleDefinitionDto> CanonicalTenantRoles =
    [
        new RoleDefinitionDto(
            RoleConstants.TenantAdmin,
            "Tenant Administrator",
            "Tenant administrator persona with full administrative permissions across tenant services.",
            PermissionConstants.GetTenantAdminPermissions()),
        new RoleDefinitionDto(
            RoleConstants.Manager,
            "Operations Manager",
            "Operations and risk supervisor persona with elevated review and approval capabilities.",
            PermissionConstants.GetDefaultManagerPermissions()),
        new RoleDefinitionDto(
            RoleConstants.Staff,
            "Tenant Staff",
            "Standard tenant operational staff persona with baseline operational capabilities.",
            PermissionConstants.GetDefaultStaffPermissions())
    ];

    [HttpGet]
    [RequirePermission(PermissionConstants.Iam.RoleRead, "iam:read")]
    public IActionResult ListRoles()
    {
        logger.LogDebug("Listed canonical roles for tenant {TenantId} by {AdminId}", currentUser.TenantId, currentUser.UserId);
        return Ok(new
        {
            Items = CanonicalTenantRoles,
            TotalItems = CanonicalTenantRoles.Count
        });
    }

    [HttpGet("{code}")]
    [RequirePermission(PermissionConstants.Iam.RoleRead, "iam:read")]
    public IActionResult GetRole([FromRoute] string code)
    {
        var role = CanonicalTenantRoles.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
        if (role == null)
        {
            return NotFound(new { detail = $"Role '{code}' not found." });
        }
        return Ok(role);
    }

    public record RoleDefinitionDto(
        string Code,
        string Name,
        string Description,
        IReadOnlyList<string> DefaultPermissions);
}
