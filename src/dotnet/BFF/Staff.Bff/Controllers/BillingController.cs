using Asp.Versioning;
using BillingService.Grpc;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;

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
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Create)]
    public async Task<IActionResult> GenerateInvoice(
        [FromBody] GenerateInvoiceBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new GenerateInvoiceRequest
            {
                TenantId = currentUser.TenantId.ToString(),
                ShipmentId = body.ShipmentId ?? string.Empty,
                CustomerId = body.CustomerId ?? string.Empty,
                PaymentTermsDays = body.PaymentTermsDays > 0 ? body.PaymentTermsDays : 30
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

    [HttpPost("/api/v{version:apiVersion}/invoices")]
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Create)]
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
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Read)]
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
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Read)]
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
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Update)]
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
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Read)]
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
    [RequirePermission(PermissionConstants.Modules.BillingSettlement, PermissionConstants.Read)]
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

public record GenerateInvoiceBody(
    string? ShipmentId,
    string? CustomerId,
    int PaymentTermsDays);

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
