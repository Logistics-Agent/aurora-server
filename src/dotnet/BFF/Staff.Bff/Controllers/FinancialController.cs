using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
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

    [HttpPost("quotes/register-shipment")]
    public async Task<IActionResult> RegisterShipmentThread(
        [FromBody] RegisterShipmentThreadBody body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.ShipmentId) || string.IsNullOrWhiteSpace(body.SourceThreadId))
        {
            return BadRequest(new { detail = "ShipmentId and SourceThreadId are required." });
        }

        try
        {
            var quote = await financialClient.RegisterShipmentThreadAsync(
                new RegisterShipmentThreadRequest
                {
                    ShipmentId = body.ShipmentId,
                    SourceThreadId = body.SourceThreadId,
                    CustomerId = body.CustomerId ?? string.Empty,
                    OriginPort = body.OriginPort ?? string.Empty,
                    DestinationPort = body.DestinationPort ?? string.Empty,
                    WeightKg = (double)(body.WeightKg ?? 1000),
                    VolumeCbm = (double)(body.VolumeCbm ?? 0),
                    TransportMode = body.TransportMode ?? "ROAD"
                },
                cancellationToken: ct);

            return Ok(MapQuote(quote));
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpPost("quotes")]
    [RequirePermission(PermissionConstants.Financial.Calculate, "financial_tax:read")]
    public async Task<IActionResult> CreateQuote(
        [FromBody] CreateQuoteBody body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.ShipmentId)
            || string.IsNullOrWhiteSpace(body.RouteId)
            || string.IsNullOrWhiteSpace(body.OriginPort)
            || string.IsNullOrWhiteSpace(body.DestinationPort)
            || body.WeightKg <= 0)
        {
            return BadRequest(new { detail = "ShipmentId, RouteId, OriginPort, DestinationPort and positive WeightKg are required." });
        }

        try
        {
            var req = new CreateQuoteRequest
            {
                SourceThreadId = body.SourceThreadId ?? string.Empty,
                ShipmentId = body.ShipmentId,
                CustomerId = body.CustomerId ?? string.Empty,
                RouteId = body.RouteId,
                RouteName = body.RouteName ?? string.Empty,
                DistanceKm = (double)(body.DistanceKm ?? 0),
                OriginCountry = body.OriginCountry ?? string.Empty,
                OriginPort = body.OriginPort,
                DestinationCountry = body.DestinationCountry ?? string.Empty,
                DestinationPort = body.DestinationPort,
                WeightKg = (double)body.WeightKg,
                VolumeCbm = (double)(body.VolumeCbm ?? 0),
                TransportMode = body.TransportMode ?? "SEA",
                CargoType = body.CargoType ?? "GENERAL",
                CargoValue = (double)(body.CargoValue ?? 0),
                Currency = body.Currency ?? "USD",
                FscRatePercent = (double)(body.FscRatePercent ?? 0),
                EbsRatePercent = (double)(body.EbsRatePercent ?? 0),
                InsuranceRatePercent = (double)(body.InsuranceRatePercent ?? 0),
                MinMarginPercent = (double)(body.MinMarginPercent ?? 10),
                TargetMarginPercent = (double)(body.TargetMarginPercent ?? 25)
            };
            if (body.HsCodes != null)
            {
                req.HsCodes.AddRange(body.HsCodes);
            }

            var quote = await financialClient.CreateQuoteAsync(req, cancellationToken: ct);
            return Ok(MapQuote(quote));
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("quotes")]
    public async Task<IActionResult> ListQuotes(
        [FromQuery] string? threadId,
        [FromQuery] string? sourceThreadId,
        [FromQuery] string? shipmentId,
        [FromQuery] string? quoteId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await financialClient.ListQuotesAsync(
                new ListQuotesRequest
                {
                    SourceThreadId = threadId ?? sourceThreadId ?? string.Empty,
                    ShipmentId = shipmentId ?? string.Empty,
                    QuoteId = quoteId ?? string.Empty
                },
                cancellationToken: ct);

            return Ok(new
            {
                quotes = response.Quotes.Select(MapQuote)
            });
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpPatch("quotes/{quoteId}/confirm")]
    public async Task<IActionResult> ConfirmQuote(
        [FromRoute] string quoteId,
        [FromBody] ConfirmQuoteBody body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(quoteId) || body.AgreedPrice <= 0)
        {
            return BadRequest(new { detail = "quoteId and positive agreedPrice are required." });
        }

        try
        {
            var quote = await financialClient.ConfirmQuoteAsync(
                new ConfirmQuoteRequest
                {
                    QuoteId = quoteId,
                    ShipmentId = body.ShipmentId ?? string.Empty,
                    AgreedPrice = (double)body.AgreedPrice
                },
                cancellationToken: ct);

            return Ok(MapQuote(quote));
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    private static object MapQuote(CommercialQuoteDto q) => new
    {
        id = q.Id,
        tenantId = q.TenantId,
        sourceThreadId = q.SourceThreadId,
        shipmentId = q.ShipmentId,
        customerId = q.CustomerId,
        routeId = q.RouteId,
        routeName = q.RouteName,
        distanceKm = q.DistanceKm,
        originCountry = q.OriginCountry,
        originPort = q.OriginPort,
        destinationCountry = q.DestinationCountry,
        destinationPort = q.DestinationPort,
        weightKg = q.WeightKg,
        volumeCbm = q.VolumeCbm,
        transportMode = q.TransportMode,
        cargoType = q.CargoType,
        cargoValue = q.CargoValue,
        hsCodes = q.HsCodes,
        baseFreightCost = q.BaseFreightCost,
        portHandlingFees = q.PortHandlingFees,
        fuelSurchargeFee = q.FuelSurchargeFee,
        cargoInsuranceFee = q.CargoInsuranceFee,
        totalCustomsFee = q.TotalCustomsFee,
        costPrice = q.CostPrice,
        minAcceptableRate = q.MinAcceptableRate,
        targetRate = q.TargetRate,
        agreedPrice = q.AgreedPrice,
        currency = q.Currency,
        calculationMethod = q.CalculationMethod,
        description = q.Description,
        status = q.Status,
        createdAt = q.CreatedAt,
        updatedAt = q.UpdatedAt
    };
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

public record RegisterShipmentThreadBody(
    string ShipmentId,
    string SourceThreadId,
    string? CustomerId,
    string? OriginPort,
    string? DestinationPort,
    decimal? WeightKg,
    decimal? VolumeCbm,
    string? TransportMode);

public record CreateQuoteBody(
    string? SourceThreadId,
    string? ShipmentId,
    string? CustomerId,
    string? OriginCountry,
    string? OriginPort,
    string? DestinationCountry,
    string? DestinationPort,
    decimal WeightKg,
    decimal? VolumeCbm,
    string? TransportMode,
    string? CargoType,
    decimal? CargoValue,
    string? Currency,
    List<string>? HsCodes,
    decimal? FscRatePercent,
    decimal? EbsRatePercent,
    decimal? InsuranceRatePercent,
    decimal? MinMarginPercent,
    decimal? TargetMarginPercent,
    string? RouteId = null,
    string? RouteName = null,
    decimal? DistanceKm = null);

public record ConfirmQuoteBody(decimal AgreedPrice, string? ShipmentId = null);
