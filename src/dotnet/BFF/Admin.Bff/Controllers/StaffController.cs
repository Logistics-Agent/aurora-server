using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Quản lý vòng đời staff trong tenant (surface chính — UsersController giữ cho backward-compat).
/// Route: /api/v1/admin/staff — chỉ TENANT_ADMIN + [RequirePermission] module iam.
/// StaffType: Normal | Operations | Documentation | CustomerService | Finance.
/// </summary>
[ApiVersion("1.0")]
public class StaffController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<StaffController> logger) : AdminControllerBase
{
    [HttpPost]
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Create)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffBody body)
    {
        try
        {
            var response = await iamClient.InviteUserAsync(
                new InviteUserRequest
                {
                    FirstName   = body.FirstName,
                    LastName    = body.LastName,
                    Email       = body.Email,
                    PhoneNumber = body.PhoneNumber ?? string.Empty,
                    StaffType   = body.StaffType ?? string.Empty,
                    RoleIds     = { body.RoleIds ?? [] }
                });

            logger.LogInformation(
                "Staff {Email} (type={StaffType}) invited to tenant {TenantId} by {AdminId}",
                body.Email, body.StaffType ?? "Normal", currentUser.TenantId, currentUser.UserId);

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
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Read)]
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
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Read)]
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
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Update)]
    public async Task<IActionResult> UpdateStaff([FromRoute] string id, [FromBody] UpdateStaffBody body)
    {
        try
        {
            var response = await iamClient.UpdateUserAsync(
                new UpdateUserRequest
                {
                    Id        = id,
                    FirstName = body.FirstName,
                    LastName  = body.LastName,
                    StaffType = body.StaffType ?? string.Empty
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
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Update)]
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
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Update)]
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
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Update)]
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

    [HttpPut("{id}/roles")]
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Assign)]
    public async Task<IActionResult> AssignRoles([FromRoute] string id, [FromBody] AssignRolesBody body)
    {
        try
        {
            var response = await iamClient.AssignRolesAsync(
                new AssignRolesRequest
                {
                    UserId  = id,
                    RoleIds = { body.RoleIds }
                });

            logger.LogInformation(
                "Roles assigned to staff {StaffId} in tenant {TenantId} by {AdminId}",
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

    // --- DTOs ---
    public record CreateStaffBody(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? StaffType,
        List<string>? RoleIds);

    public record UpdateStaffBody(string FirstName, string LastName, string? StaffType);

    public record AssignRolesBody(List<string> RoleIds);

    private static object MapUserResponse(UserResponse r) => new
    {
        r.Id,
        r.FirstName,
        r.LastName,
        r.Email,
        r.PhoneNumber,
        Status      = r.Status.ToString(),
        r.StaffType,
        r.RoleIds,
        SystemRoles = r.SystemRoles.Select(sr => sr.ToString()),
        CreatedAt   = r.CreatedAt?.ToDateTime(),
        UpdatedAt   = r.UpdatedAt?.ToDateTime()
    };
}
