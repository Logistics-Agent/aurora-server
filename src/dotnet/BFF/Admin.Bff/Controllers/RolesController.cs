using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Roles là READ-ONLY theo thiết kế — không hỗ trợ tạo/sửa/xóa role qua API.
/// (System roles được seed sẵn: SYSTEM_ADMIN, TENANT_ADMIN, TENANT_STAFF.)
/// Route: /api/v1/admin/roles
/// </summary>
[ApiVersion("1.0")]
public class RolesController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<RolesController> logger) : AdminControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionConstants.Iam.RoleRead, "iam:read")]
    public async Task<IActionResult> ListRoles([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        var response = await iamClient.GetManyRolesAsync(
            new GetManyRolesRequest { Page = page, Limit = limit });

        logger.LogDebug("Listed roles for tenant {TenantId} by {AdminId}", currentUser.TenantId, currentUser.UserId);

        return Ok(new
        {
            Items = response.Roles.Select(MapRoleResponse),
            response.Page,
            response.Limit,
            response.TotalItems,
            response.TotalPages
        });
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Iam.RoleRead, "iam:read")]
    public async Task<IActionResult> GetRole([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetRoleAsync(
                new GetRoleRequest { Id = id });

            return Ok(MapRoleResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Role '{id}' not found." });
        }
    }

    // --- DTOs ---
    private static object MapRoleResponse(RoleResponse r) => new
    {
        r.Id,
        r.Code,
        r.Name,
        r.Description,
        r.PermissionIds,
        CreatedAt = r.CreatedAt?.ToDateTime(),
        UpdatedAt = r.UpdatedAt?.ToDateTime()
    };
}
