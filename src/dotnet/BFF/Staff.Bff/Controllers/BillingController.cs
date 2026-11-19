using Asp.Versioning;
using BillingService.Grpc;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;
using System.Globalization;
using BuildingBlocks.BFF.Extensions;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý Hóa đơn, Kiểm tra hạn mức tín dụng và Ví Ký quỹ Escrow (Billing & Settlement Service).
/// Route: /api/v1/billing, /api/v1/invoices, /api/v1/escrow
/// </summary>
[ApiVersion("1.0")]
public class BillingController(
    BillingService.Grpc.BillingService.BillingServiceClient billingClient,
    ICurrentUserService currentUser,
    ILogger<BillingController> logger) : StaffControllerBase
{
    [HttpPost("/api/v{version:apiVersion}/invoices/generate")]
    [RequirePermission(PermissionConstants.Billing.InvoiceCreate, "billing_settlement:create")]
    public async Task<IActionResult> GenerateInvoice(
        [FromBody] GenerateInvoiceBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new GenerateInvoiceRequest
            {
                TenantId = currentUser.TenantId.ToString(),
                ShipmentId = body.ShipmentId ?? string.Empty
            };

            var response = await billingClient.GenerateInvoiceAsync(req, cancellationToken: ct);
            return Created($"/api/v1/invoices/{response.Id}", response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("/api/v{version:apiVersion}/billing/shipment-agreements")]
    [RequirePermission(PermissionConstants.Billing.SettlementManage)]
    public async Task<IActionResult> RecordShipmentAgreement(
        [FromBody] RecordShipmentAgreementBody body,
        CancellationToken ct = default)
    {
        if (body.Subtotal <= 0 || body.TaxAmount < 0 ||
            decimal.Round(body.Subtotal, 2) != body.Subtotal ||
            decimal.Round(body.TaxAmount, 2) != body.TaxAmount)
            return BadRequest(new { detail = "Subtotal must be positive and money may have at most two decimal places." });
        try
        {
            var response = await billingClient.RecordShipmentAgreementAsync(new RecordShipmentAgreementRequest
            {
                ShipmentId = body.ShipmentId ?? string.Empty,
                CustomerId = body.CustomerId ?? string.Empty,
                Subtotal = body.Subtotal.ToString("0.00", CultureInfo.InvariantCulture),
                TaxAmount = body.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture),
                Currency = body.Currency ?? string.Empty,
                EvidenceReference = body.EvidenceReference ?? string.Empty
            }, cancellationToken: ct);
            return Created($"/api/v1/billing/shipment-agreements/{response.ShipmentId}", response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    [HttpGet("/api/v{version:apiVersion}/billing/shipment-agreements/{shipmentId}")]
    [RequirePermission(PermissionConstants.Billing.Read)]
    public async Task<IActionResult> GetShipmentAgreement(
        [FromRoute] string shipmentId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await billingClient.GetShipmentAgreementAsync(
                new GetShipmentAgreementRequest { ShipmentId = shipmentId }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    [HttpPut("/api/v{version:apiVersion}/billing/customer-credit/{customerId}")]
    [RequirePermission(PermissionConstants.Billing.SettlementManage)]
    public async Task<IActionResult> SetCustomerCreditAccount(
        [FromRoute] string customerId,
        [FromBody] SetCustomerCreditAccountBody body,
        CancellationToken ct = default)
    {
        if (body.CreditLimit < 0 || decimal.Round(body.CreditLimit, 2) != body.CreditLimit)
            return BadRequest(new { detail = "Credit limit must be non-negative with at most two decimal places." });
        try
        {
            var response = await billingClient.SetCustomerCreditAccountAsync(new SetCustomerCreditAccountRequest
            {
                CustomerId = customerId,
                CreditLimit = body.CreditLimit.ToString("0.00", CultureInfo.InvariantCulture),
                Currency = body.Currency ?? string.Empty,
                TermsDays = body.TermsDays,
                OnHold = body.OnHold,
                Reason = body.Reason ?? string.Empty
            }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) { return ex.ToActionResult(); }
    }

    [HttpPost("/api/v{version:apiVersion}/invoices")]
    [RequirePermission(PermissionConstants.Billing.InvoiceCreate, "billing_settlement:create")]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateInvoiceBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new CreateInvoiceRequest
            {
                TenantId = currentUser.TenantId.ToString(),
                ShipmentId = body.ShipmentId ?? string.Empty,
                CustomerId = body.CustomerId ?? string.Empty,
                DueDate = body.DueDate ?? string.Empty
            };

            if (body.Items != null)
            {
                req.Items.AddRange(body.Items.Select(i => new InvoiceLineItemInput
                {
                    Description = i.Description ?? string.Empty,
                    Amount = (double)i.Amount,
                    Quantity = i.Quantity,
                    UnitPrice = (double)i.UnitPrice,
                    Category = i.Category ?? "FREIGHT"
                }));
            }

            var response = await billingClient.CreateInvoiceAsync(req, cancellationToken: ct);
            return Created($"/api/v1/invoices/{response.Id}", response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("/api/v{version:apiVersion}/invoices/{id}")]
    [RequirePermission(PermissionConstants.Billing.Read)]
    public async Task<IActionResult> GetInvoiceDetail(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var response = await billingClient.GetInvoiceDetailAsync(new GetInvoiceRequest { InvoiceId = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("/api/v{version:apiVersion}/invoices")]
    [RequirePermission(PermissionConstants.Billing.Read)]
    public async Task<IActionResult> ListInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var req = new ListInvoicesRequest
        {
            TenantId = currentUser.TenantId.ToString(),
            Page = page,
            Limit = limit,
            Status = status ?? string.Empty
        };

        var response = await billingClient.ListInvoicesAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPatch("/api/v{version:apiVersion}/invoices/{id}/status")]
    [RequirePermission(PermissionConstants.Billing.InvoiceUpdate, "billing_settlement:update")]
    public async Task<IActionResult> UpdateInvoiceStatus(
        [FromRoute] string id,
        [FromBody] UpdateInvoiceStatusBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new UpdateInvoiceStatusRequest
            {
                InvoiceId = id,
                Status = body.Status ?? string.Empty
            };
            var response = await billingClient.UpdateInvoiceStatusAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("/api/v{version:apiVersion}/billing/credit-check")]
    [RequirePermission(PermissionConstants.Billing.CreditCheck, "billing_settlement:read")]
    public async Task<IActionResult> CheckCustomerCredit(
        [FromBody] CreditCheckBody body,
        CancellationToken ct = default)
    {
        var req = new CreditCheckRequest
        {
            TenantId = currentUser.TenantId.ToString(),
            CustomerId = body.CustomerId ?? string.Empty,
            NewAmount = (double)body.NewAmount
        };

        var response = await billingClient.CheckCustomerCreditAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpGet("/api/v{version:apiVersion}/escrow/wallets/{id}")]
    [RequirePermission(PermissionConstants.Billing.EscrowRead, "billing_settlement:read")]
    public async Task<IActionResult> GetWalletBalance(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var response = await billingClient.GetWalletBalanceAsync(new GetWalletBalanceRequest { WalletId = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record GenerateInvoiceBody(string? ShipmentId);

public record RecordShipmentAgreementBody(
    string? ShipmentId, string? CustomerId, decimal Subtotal, decimal TaxAmount,
    string? Currency, string? EvidenceReference);

public record SetCustomerCreditAccountBody(
    decimal CreditLimit, string? Currency, int TermsDays, bool OnHold, string? Reason);

public record CreateInvoiceBody(
    string? ShipmentId,
    string? CustomerId,
    string? DueDate,
    List<InvoiceLineItemBody>? Items);

public record InvoiceLineItemBody(
    string? Description,
    decimal Amount,
    int Quantity,
    decimal UnitPrice,
    string? Category);

public record UpdateInvoiceStatusBody(string? Status);
public record CreditCheckBody(string? CustomerId, decimal NewAmount);
