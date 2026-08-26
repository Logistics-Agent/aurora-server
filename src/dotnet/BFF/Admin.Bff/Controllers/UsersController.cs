using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Common.Grpc;
using Grpc.Core;
using Shared.Constants;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Giữ cho backward-compat — surface chính cho staff lifecycle là StaffController (/api/v1/admin/staff).
/// </summary>
[ApiVersion("1.0")]
public class UsersController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<UsersController> logger) : AdminControllerBase
{
    [HttpPost("invite")]
    [RequirePermission(PermissionConstants.Iam.UserInvite, "iam:create")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserBody body)
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
                    RoleIds     = { body.RoleIds }
                });

            logger.LogInformation(
                "User {Email} invited to tenant {TenantId} by {AdminId}",
                body.Email, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/users/{response.Id}", MapUserResponse(response));
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

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Iam.UserRead, "iam:read")]
    public async Task<IActionResult> GetUser([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetUserAsync(
                new GetUserRequest { Id = id });

            return Ok(MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"User '{id}' not found." });
        }
    }

    // --- DTOs ---
    public record InviteUserBody(string FirstName, string LastName, string Email, string? PhoneNumber, string? StaffType, List<string> RoleIds);

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
