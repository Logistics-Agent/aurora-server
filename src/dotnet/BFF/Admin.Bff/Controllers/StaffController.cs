using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Constants;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Quản lý vòng đời staff & phân quyền direct permissions trong tenant.
/// Route: /api/v1/admin/staff — phân quyền bằng [RequirePermission] module iam.
/// </summary>
[ApiVersion("1.0")]
public class StaffController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<StaffController> logger) : AdminControllerBase
{
    [HttpPost]
    [RequirePermission(PermissionConstants.Iam.UserInvite, "iam:create")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffBody body)
    {
        try
        {
            var req = new InviteUserRequest
            {
                FirstName = body.FirstName,
                LastName = body.LastName,
                Email = body.Email,
                PhoneNumber = body.PhoneNumber ?? string.Empty,
                Role = string.IsNullOrWhiteSpace(body.Role) ? RoleConstants.Staff : body.Role,
                ApplyDefaultPermissions = body.ApplyDefaultPermissions
            };

            if (body.Permissions != null && body.Permissions.Count > 0)
            {
                req.Permissions.AddRange(body.Permissions);
            }

            var response = await iamClient.InviteUserAsync(req);

            logger.LogInformation(
                "Staff {Email} (role={Role}) invited to tenant {TenantId} by {AdminId}",
                body.Email, req.Role, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/staff/{response.Id}", MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = "A user with this email already exists in the tenant." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.Iam.UserRead, "iam:read")]
    public async Task<IActionResult> ListStaff([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        var response = await iamClient.GetManyUsersAsync(
            new GetManyUsersRequest { Page = page, Limit = limit });

        return Ok(new
        {
            Items = response.Users.Select(MapUserResponse),
            response.Page,
            response.Limit,
            response.TotalItems,
            response.TotalPages
        });
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Iam.UserRead, "iam:read")]
    public async Task<IActionResult> GetStaff([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetUserAsync(new GetUserRequest { Id = id });
            return Ok(MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionConstants.Iam.UserUpdate, "iam:update")]
    public async Task<IActionResult> UpdateStaff([FromRoute] string id, [FromBody] UpdateStaffBody body)
    {
        try
        {
            var response = await iamClient.UpdateUserAsync(
                new UpdateUserRequest
                {
                    Id = id,
                    FirstName = body.FirstName,
                    LastName = body.LastName
                });

            logger.LogInformation(
                "Staff {StaffId} updated in tenant {TenantId} by {AdminId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("{id}/activate")]
    [RequirePermission(PermissionConstants.Iam.UserUpdate, "iam:update")]
    public async Task<IActionResult> ActivateStaff([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.ActivateUserAsync(new ActivateUserRequest { UserId = id });

            logger.LogInformation(
                "Staff {StaffId} activated in tenant {TenantId} by {AdminId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
    }

    [HttpPost("{id}/deactivate")]
    [RequirePermission(PermissionConstants.Iam.UserUpdate, "iam:update")]
    public async Task<IActionResult> DeactivateStaff([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.SuspendUserAsync(new SuspendUserRequest { UserId = id });

            logger.LogInformation(
                "Staff {StaffId} deactivated in tenant {TenantId} by {AdminId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
    }

    [HttpPost("{id}/reset-password")]
    [RequirePermission(PermissionConstants.Iam.UserUpdate, "iam:update")]
    public async Task<IActionResult> ResetPassword([FromRoute] string id)
    {
        try
        {
            await iamClient.ResetUserPasswordAsync(new ResetUserPasswordRequest { UserId = id });

            logger.LogInformation(
                "Password reset requested for staff {StaffId} by {AdminId}",
                id, currentUser.UserId);

            return NoContent();
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
    }

    /// <summary>
    /// PATCH /api/v1/admin/staff/{id}/role — thay đổi single base role cho user.
    /// Giữ nguyên permissions hiện tại; nếu ApplyDefaultPermissions = true sẽ union defaults.
    /// </summary>
    [HttpPatch("{id}/role")]
    [RequirePermission(PermissionConstants.Iam.RoleManage, "iam:assign")]
    public async Task<IActionResult> UpdateStaffRole([FromRoute] string id, [FromBody] UpdateStaffRoleBody body)
    {
        try
        {
            var response = await iamClient.UpdateUserRoleAsync(
                new UpdateUserRoleRequest
                {
                    UserId = id,
                    NewRole = body.Role,
                    ApplyDefaultPermissions = body.ApplyDefaultPermissions
                });

            logger.LogInformation(
                "Role for staff {StaffId} updated to {Role} in tenant {TenantId} by {AdminId}",
                id, body.Role, currentUser.TenantId, currentUser.UserId);

            return Ok(new
            {
                response.UserId,
                response.Role,
                Permissions = response.Permissions.ToList(),
                response.PermissionVersion,
                ElevatedPermissionsRetained = response.ElevatedPermissionsRetained.ToList()
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>
    /// GET /api/v1/admin/staff/{id}/permissions — lấy thông tin authorization (Role + Direct Permissions).
    /// </summary>
    [HttpGet("{id}/permissions")]
    [RequirePermission(PermissionConstants.Iam.UserRead, "iam:read")]
    public async Task<IActionResult> GetStaffPermissions([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetUserPermissionsAsync(
                new GetUserPermissionsRequest { UserId = id });

            return Ok(new
            {
                response.UserId,
                response.Role,
                Permissions = response.Permissions.ToList(),
                response.PermissionVersion
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
    }

    /// <summary>
    /// PATCH /api/v1/admin/staff/{id}/permissions — cập nhật delta (grant/revoke) quyền trực tiếp cho 1 user.
    /// </summary>
    [HttpPatch("{id}/permissions")]
    [RequirePermission(PermissionConstants.Iam.PermissionManage, "iam:assign")]
    public async Task<IActionResult> UpdateStaffPermissions([FromRoute] string id, [FromBody] UpdateStaffPermissionsBody body)
    {
        try
        {
            var req = new UpdateUserPermissionsRequest { UserId = id };
            if (body.Grant != null) req.Grant.AddRange(body.Grant);
            if (body.Revoke != null) req.Revoke.AddRange(body.Revoke);

            var response = await iamClient.UpdateUserPermissionsAsync(req);

            logger.LogInformation(
                "Direct permissions updated for staff {StaffId} in tenant {TenantId} by {AdminId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(new
            {
                response.UserId,
                response.Role,
                Permissions = response.Permissions.ToList(),
                response.PermissionVersion
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Staff '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>
    /// PATCH /api/v1/admin/staff/permissions — cập nhật bulk delta permissions cho nhiều users.
    /// </summary>
    [HttpPatch("permissions")]
    [RequirePermission(PermissionConstants.Iam.PermissionManage, "iam:assign")]
    public async Task<IActionResult> BulkUpdateStaffPermissions([FromBody] BulkUpdateStaffPermissionsBody body)
    {
        try
        {
            var req = new BulkUpdateUserPermissionsRequest();
            if (body.UserIds != null) req.UserIds.AddRange(body.UserIds);
            if (body.Grant != null) req.Grant.AddRange(body.Grant);
            if (body.Revoke != null) req.Revoke.AddRange(body.Revoke);

            var response = await iamClient.BulkUpdateUserPermissionsAsync(req);

            logger.LogInformation(
                "Bulk direct permissions updated for {Count} staff users in tenant {TenantId} by {AdminId}",
                response.UpdatedUsersCount, currentUser.TenantId, currentUser.UserId);

            return Ok(new
            {
                response.UpdatedUsersCount,
                AffectedUserIds = response.AffectedUserIds.ToList()
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    // --- DTOs ---
    public record CreateStaffBody(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber = null,
        string? Role = RoleConstants.Staff,
        bool ApplyDefaultPermissions = true,
        List<string>? Permissions = null);

    public record UpdateStaffBody(string FirstName, string LastName);

    public record UpdateStaffRoleBody(string Role, bool ApplyDefaultPermissions = false);

    public record UpdateStaffPermissionsBody(List<string>? Grant = null, List<string>? Revoke = null);

    public record BulkUpdateStaffPermissionsBody(List<string> UserIds, List<string>? Grant = null, List<string>? Revoke = null);

    private static object MapUserResponse(UserResponse r) => new
    {
        r.Id,
        r.FirstName,
        r.LastName,
        r.Email,
        r.PhoneNumber,
        Status = r.Status.ToString(),
        r.Role,
        Permissions = r.Permissions.ToList(),
        r.PermissionVersion,
        r.TenantId,
        CreatedAt = r.CreatedAt?.ToDateTime(),
        UpdatedAt = r.UpdatedAt?.ToDateTime()
    };
}
