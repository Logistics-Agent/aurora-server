using System.Globalization;
using Asp.Versioning;
using BillingService.Grpc;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

[ApiVersion("1.0")]
public sealed class ShipmentQuotesController(
    BillingService.Grpc.BillingService.BillingServiceClient billingClient,
    ICurrentUserService currentUser,
    IHostEnvironment environment,
    IConfiguration configuration) : StaffControllerBase
{
    [HttpPost("/api/v{version:apiVersion}/billing/shipment-quotes")]
    [RequirePermission(PermissionConstants.Billing.QuoteCreate)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateShipmentQuoteBody body, CancellationToken ct)
    {
        if (body.ListPrice <= 0 || body.FloorPrice <= 0 || body.FloorPrice > body.ListPrice ||
            decimal.Round(body.ListPrice, 2) != body.ListPrice ||
            decimal.Round(body.FloorPrice, 2) != body.FloorPrice)
            return BadRequest(new { detail = "Valid list and floor prices with at most two decimal places are required." });
        try
        {
            var response = await billingClient.CreateShipmentQuoteDraftAsync(
                new CreateShipmentQuoteDraftRequest
                {
                    ShipmentId = body.ShipmentId ?? string.Empty,
                    CustomerId = body.CustomerId ?? string.Empty,
                    Currency = body.Currency ?? string.Empty,
                    ListPrice = body.ListPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    FloorPrice = body.FloorPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    EvidenceReference = body.EvidenceReference ?? string.Empty,
                    ValidFrom = body.ValidFrom.ToUniversalTime().ToString("O"),
                    ValidUntil = body.ValidUntil.ToUniversalTime().ToString("O")
                }, Headers(PermissionConstants.Billing.QuoteCreate), cancellationToken: ct);
            return Created($"/api/v1/billing/shipment-quotes/{response.Id}", response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    [HttpGet("/api/v{version:apiVersion}/billing/shipment-quotes/{shipmentId}")]
    [RequirePermission(PermissionConstants.Billing.QuoteRead)]
    public async Task<IActionResult> List([FromRoute] string shipmentId, [FromQuery] string? customerId,
        CancellationToken ct)
    {
        try
        {
            var response = await billingClient.ListShipmentQuotesAsync(
                new ListShipmentQuotesRequest { ShipmentId = shipmentId, CustomerId = customerId ?? string.Empty },
                Headers(PermissionConstants.Billing.QuoteRead), cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    [HttpPost("/api/v{version:apiVersion}/billing/shipment-quotes/{quoteId}/approve")]
    [RequirePermission(PermissionConstants.Billing.QuoteApprove)]
    public async Task<IActionResult> Approve([FromRoute] string quoteId, CancellationToken ct)
    {
        try
        {
            var response = await billingClient.ApproveShipmentQuoteAsync(
                new ShipmentQuoteActionRequest { QuoteId = quoteId },
                Headers(PermissionConstants.Billing.QuoteApprove), cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    [HttpPost("/api/v{version:apiVersion}/billing/shipment-quotes/{quoteId}/revoke")]
    [RequirePermission(PermissionConstants.Billing.QuoteApprove)]
    public async Task<IActionResult> Revoke([FromRoute] string quoteId,
        [FromBody] RevokeShipmentQuoteBody body, CancellationToken ct)
    {
        try
        {
            var response = await billingClient.RevokeShipmentQuoteAsync(
                new RevokeShipmentQuoteRequest { QuoteId = quoteId, Reason = body.Reason ?? string.Empty },
                Headers(PermissionConstants.Billing.QuoteApprove), cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    private Metadata Headers(string permission)
    {
        if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue ||
            !currentUser.HasPermission(permission))
            throw new UnauthorizedAccessException("Quote permission and identity are required.");
        var secret = configuration["INTERNAL_SERVICE_SECRET"];
        if (string.IsNullOrWhiteSpace(secret) && !environment.IsDevelopment())
            throw new InvalidOperationException("INTERNAL_SERVICE_SECRET must be configured for Billing quote calls.");
        var headers = new Metadata
        {
            { "x-tenant-id", currentUser.TenantId.Value.ToString() },
            { "x-user-id", currentUser.UserId.Value.ToString() },
            { "x-permissions", permission }
        };
        if (!string.IsNullOrWhiteSpace(secret)) headers.Add("x-internal-secret", secret);
        return headers;
    }
}

public sealed record CreateShipmentQuoteBody(
    string? ShipmentId, string? CustomerId, string? Currency,
    decimal ListPrice, decimal FloorPrice, string? EvidenceReference,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);

public sealed record RevokeShipmentQuoteBody(string? Reason);
