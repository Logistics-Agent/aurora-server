using Asp.Versioning;
using Common.Grpc;
using Grpc.Core;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace SystemBff.Controllers;

/// <summary>
/// Quản lý Tenant ở cấp độ System.
/// Route: /api/v1/system/tenants
/// Quyền: Chỉ dành cho SYSTEM_ADMIN (role gate ở SystemControllerBase — không cần [RequirePermission]).
/// </summary>
[ApiVersion("1.0")]
public class TenantsController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<TenantsController> logger) : SystemControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantBody body)
    {
        try
        {
            var response = await iamClient.CreateTenantAsync(
                new CreateTenantRequest
                {
                    Name           = body.Name,
                    TenantCode     = body.TenantCode,
                    AdminEmail     = body.AdminEmail,
                    AdminFirstName = body.AdminFirstName,
                    AdminLastName  = body.AdminLastName,
                    PlanType       = body.PlanType,
                    CompanyDomain  = body.CompanyDomain
                });

            logger.LogInformation(
                "Tenant {TenantCode} created by SystemAdmin {UserId}",
                body.TenantCode, currentUser.UserId);

            return Created($"/api/v1/system/tenants/{response.Id}", MapTenantResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = "Tenant code already exists." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListTenants([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        var response = await iamClient.ListTenantsAsync(
            new ListTenantsRequest { Page = page, Limit = limit });

        return Ok(new
        {
            Items = response.Tenants.Select(MapTenantResponse),
            response.Page,
            response.Limit,
            response.TotalItems,
            response.TotalPages
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTenant([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetTenantAsync(
                new GetTenantRequest { Id = id });

            return Ok(MapTenantResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Tenant '{id}' not found." });
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateTenantStatus(
        [FromRoute] string id,
        [FromBody] UpdateTenantStatusBody body)
    {
        try
        {
            var response = await iamClient.UpdateTenantStatusAsync(
                new UpdateTenantStatusRequest
                {
                    TenantId = id,
                    Status   = body.Status
                });

            logger.LogInformation(
                "Tenant {TenantId} status updated to {Status} by SystemAdmin {UserId}",
                id, body.Status, currentUser.UserId);

            return Ok(MapTenantResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Tenant '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTenant([FromRoute] string id)
    {
        try
        {
            await iamClient.DeleteTenantAsync(new DeleteTenantRequest { Id = id });

            logger.LogInformation(
                "Tenant {TenantId} deleted (soft) by SystemAdmin {UserId}",
                id, currentUser.UserId);

            return NoContent();
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Tenant '{id}' not found." });
        }
    }

    // --- DTOs ---
    public record CreateTenantBody(
        string Name,
        string TenantCode,
        string AdminEmail,
        string AdminFirstName,
        string AdminLastName,
        PlanType PlanType,
        string CompanyDomain);

    public record UpdateTenantStatusBody(TenantStatus Status);

    private static object MapTenantResponse(TenantResponse r) => new
    {
        r.Id,
        r.Name,
        r.TenantCode,
        PlanType  = r.PlanType.ToString(),
        Status    = r.Status.ToString(),
        r.AdminEmail,
        CreatedAt = r.CreatedAt?.ToDateTime(),
        UpdatedAt = r.UpdatedAt?.ToDateTime()
    };
}
