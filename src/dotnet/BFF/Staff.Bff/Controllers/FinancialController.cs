using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using FinancialService.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Tính toán ước lượng chi phí cước và thuế quan hải quan (Financial & Tax Service).
/// Route: /api/v1/financial
/// </summary>
[ApiVersion("1.0")]
public class FinancialController(
    FinancialService.Grpc.FinancialService.FinancialServiceClient financialClient,
    ICurrentUserService currentUser,
    ILogger<FinancialController> logger) : StaffControllerBase
{
    [HttpPost("estimate-cost")]
    [RequirePermission(PermissionConstants.Financial.Calculate, "financial_tax:read")]
    public async Task<IActionResult> EstimateCost(
        [FromBody] EstimateCostBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new EstimateCostRequest
            {
                OriginCountry = body.OriginCountry ?? string.Empty,
                OriginPort = body.OriginPort ?? string.Empty,
                DestinationCountry = body.DestinationCountry ?? string.Empty,
                DestinationPort = body.DestinationPort ?? string.Empty,
                WeightKg = (double)body.WeightKg,
                VolumeCbm = (double)body.VolumeCbm,
                LengthCm = (double)body.LengthCm,
                WidthCm = (double)body.WidthCm,
                HeightCm = (double)body.HeightCm,
                TransportMode = body.TransportMode ?? "SEA",
                CargoType = body.CargoType ?? "GENERAL",
                CargoValue = (double)body.CargoValue,
                Currency = body.Currency ?? "USD"
            };

            if (body.HsCodes != null)
            {
                req.HsCodes.AddRange(body.HsCodes);
            }

            var response = await financialClient.EstimateCostAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("customs-duty")]
    [RequirePermission(PermissionConstants.Financial.Calculate, "financial_tax:read")]
    public async Task<IActionResult> GetCustomsDuty(
        [FromBody] CustomsDutyBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new GetCustomsDutyRequest
            {
                OriginCountry = body.OriginCountry ?? string.Empty,
                DestinationCountry = body.DestinationCountry ?? string.Empty,
                HsCode = body.HsCode ?? string.Empty,
                CargoValue = (double)body.CargoValue
            };

            var response = await financialClient.GetCustomsDutyAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record EstimateCostBody(
    string? OriginCountry,
    string? OriginPort,
    string? DestinationCountry,
    string? DestinationPort,
    decimal WeightKg,
    decimal VolumeCbm,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    string? TransportMode,
    string? CargoType,
    decimal CargoValue,
    string? Currency,
    List<string>? HsCodes);

public record CustomsDutyBody(
    string? OriginCountry,
    string? DestinationCountry,
    string? HsCode,
    decimal CargoValue);
