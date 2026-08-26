using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;
using ShipmentWorkflow.Grpc;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý vận đơn (Shipment Workflow Service).
/// Route: /api/v1/shipments — phân quyền chi tiết bằng [RequirePermission].
/// </summary>
[ApiVersion("1.0")]
public class ShipmentsController(
    ShipmentWorkflowService.ShipmentWorkflowServiceClient shipmentClient,
    ICurrentUserService currentUser,
    ILogger<ShipmentsController> logger) : StaffControllerBase
{
    [HttpPost]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new CreateShipmentRequest
            {
                OrderId = body.OrderId ?? string.Empty,
                CustomerName = body.CustomerName ?? string.Empty,
                OriginAddress = body.OriginAddress ?? string.Empty,
                DestinationAddress = body.DestinationAddress ?? string.Empty,
                OriginCountry = body.OriginCountry ?? string.Empty,
                DestinationCountry = body.DestinationCountry ?? string.Empty
            };

            if (body.CargoItems != null)
            {
                req.CargoItems.AddRange(body.CargoItems.Select(c => new CargoItemRequest
                {
                    Name = c.Name ?? string.Empty,
                    Quantity = c.Quantity,
                    WeightKg = (double)c.WeightKg,
                    HsCode = c.HsCode ?? string.Empty
                }));
            }

            var response = await shipmentClient.CreateShipmentAsync(req, cancellationToken: ct);
            logger.LogInformation("Shipment {ShipmentId} created for tenant {TenantId} by {UserId}",
                response.Id, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/shipments/{response.Id}", response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    public async Task<IActionResult> GetShipment([FromRoute] string id, CancellationToken ct = default)
    {
        try
        {
            var response = await shipmentClient.GetShipmentAsync(new GetShipmentRequest { Id = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    public async Task<IActionResult> ListShipments(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? shipmentNo = null,
        [FromQuery] string? customerName = null,
        [FromQuery] DateTimeOffset? createdFrom = null,
        [FromQuery] DateTimeOffset? createdTo = null,
        CancellationToken ct = default)
    {
        var req = new ListShipmentsRequest
        {
            Page = page,
            Limit = limit,
            Status = status ?? string.Empty,
            ShipmentNo = shipmentNo ?? string.Empty,
            CustomerName = customerName ?? string.Empty,
            CreatedFrom = createdFrom.HasValue ? Timestamp.FromDateTimeOffset(createdFrom.Value) : null,
            CreatedTo = createdTo.HasValue ? Timestamp.FromDateTimeOffset(createdTo.Value) : null
        };

        var response = await shipmentClient.ListShipmentsAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateShipment([FromRoute] string id, [FromBody] UpdateShipmentBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new UpdateShipmentRequest
            {
                Id = id,
                CustomerName = body.CustomerName ?? string.Empty,
                DestinationAddress = body.DestinationAddress ?? string.Empty,
                Priority = body.Priority ?? string.Empty,
                TransportMode = body.TransportMode ?? string.Empty,
                Notes = body.Notes ?? string.Empty
            };

            var response = await shipmentClient.UpdateShipmentAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(412, new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("{id}/submit")]
    [RequirePermission(PermissionConstants.Shipment.Submit, "documents:update")]
    public async Task<IActionResult> SubmitShipment([FromRoute] string id, CancellationToken ct = default)
    {
        try
        {
            var response = await shipmentClient.SubmitShipmentAsync(new SubmitShipmentRequest { Id = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(412, new { detail = ex.Status.Detail });
        }
    }

    [HttpPatch("{id}/status")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateShipmentStatus([FromRoute] string id, [FromBody] UpdateStatusBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new UpdateShipmentStatusRequest
            {
                Id = id,
                Status = body.Status ?? string.Empty,
                Note = body.Note ?? string.Empty
            };
            var response = await shipmentClient.UpdateShipmentStatusAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("{id}/cancel")]
    [RequirePermission(PermissionConstants.Shipment.Cancel, "documents:update")]
    public async Task<IActionResult> CancelShipment([FromRoute] string id, [FromBody] CancelShipmentBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new CancelShipmentRequest { Id = id, Reason = body.Reason ?? string.Empty };
            var response = await shipmentClient.CancelShipmentAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> DeleteDraftShipment([FromRoute] string id, CancellationToken ct = default)
    {
        try
        {
            var response = await shipmentClient.DeleteDraftShipmentAsync(new DeleteDraftShipmentRequest { Id = id }, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(412, new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("import")]
    [RequirePermission(PermissionConstants.Shipment.Import, "documents:import")]
    public async Task<IActionResult> ImportShipments([FromBody] ImportShipmentsBody body, CancellationToken ct = default)
    {
        var req = new ImportShipmentsRequest
        {
            FileName = body.FileName ?? "import.csv",
            Content = body.Content ?? string.Empty,
            ImportRequestId = body.ImportRequestId ?? Guid.NewGuid().ToString()
        };

        var response = await shipmentClient.ImportShipmentsAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/cargo")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AddCargoItem([FromRoute] string id, [FromBody] CargoItemDto item, CancellationToken ct = default)
    {
        var req = new AddCargoItemRequest
        {
            ShipmentId = id,
            Name = item.Name ?? string.Empty,
            Quantity = item.Quantity,
            WeightKg = (double)item.WeightKg,
            HsCode = item.HsCode ?? string.Empty
        };
        var response = await shipmentClient.AddCargoItemAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("{id}/cargo/{itemId}")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateCargoItem([FromRoute] string id, [FromRoute] string itemId, [FromBody] CargoItemDto item, CancellationToken ct = default)
    {
        var req = new UpdateCargoItemRequest
        {
            ShipmentId = id,
            CargoItemId = itemId,
            Name = item.Name ?? string.Empty,
            Quantity = item.Quantity,
            WeightKg = (double)item.WeightKg,
            HsCode = item.HsCode ?? string.Empty
        };
        var response = await shipmentClient.UpdateCargoItemAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("{id}/cargo/{itemId}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> RemoveCargoItem([FromRoute] string id, [FromRoute] string itemId, CancellationToken ct = default)
    {
        var response = await shipmentClient.RemoveCargoItemAsync(new RemoveCargoItemRequest { ShipmentId = id, CargoItemId = itemId }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/locations")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AddShipmentLocation([FromRoute] string id, [FromBody] LocationDto loc, CancellationToken ct = default)
    {
        var req = new AddShipmentLocationRequest
        {
            ShipmentId = id,
            Type = loc.Type ?? string.Empty,
            Name = loc.Name ?? string.Empty,
            Address = loc.Address ?? string.Empty,
            Sequence = loc.Sequence,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ContactName = loc.ContactName ?? string.Empty,
            ContactPhone = loc.ContactPhone ?? string.Empty
        };
        var response = await shipmentClient.AddShipmentLocationAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("{id}/locations/{locationId}")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    public async Task<IActionResult> UpdateShipmentLocation([FromRoute] string id, [FromRoute] string locationId, [FromBody] LocationDto loc, CancellationToken ct = default)
    {
        var req = new UpdateShipmentLocationRequest
        {
            ShipmentId = id,
            LocationId = locationId,
            Type = loc.Type ?? string.Empty,
            Name = loc.Name ?? string.Empty,
            Address = loc.Address ?? string.Empty,
            Sequence = loc.Sequence,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ContactName = loc.ContactName ?? string.Empty,
            ContactPhone = loc.ContactPhone ?? string.Empty
        };
        var response = await shipmentClient.UpdateShipmentLocationAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("{id}/locations/{locationId}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> RemoveShipmentLocation([FromRoute] string id, [FromRoute] string locationId, CancellationToken ct = default)
    {
        var response = await shipmentClient.RemoveShipmentLocationAsync(new RemoveShipmentLocationRequest { ShipmentId = id, LocationId = locationId }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/documents")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AttachShipmentDocument([FromRoute] string id, [FromBody] DocumentDto doc, CancellationToken ct = default)
    {
        var req = new AttachShipmentDocumentRequest
        {
            ShipmentId = id,
            FileName = doc.FileName ?? string.Empty,
            DocumentType = doc.DocumentType ?? string.Empty,
            StorageUrl = doc.StorageUrl ?? string.Empty,
            OcrStatus = doc.OcrStatus ?? string.Empty,
            ExtractedDataJson = doc.ExtractedDataJson ?? "{}"
        };
        if (doc.OcrConfidence.HasValue)
        {
            req.OcrConfidence = doc.OcrConfidence.Value;
        }

        var response = await shipmentClient.AttachShipmentDocumentAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("{id}/documents/{documentId}")]
    [RequirePermission(PermissionConstants.Shipment.Delete, "documents:delete")]
    public async Task<IActionResult> RemoveShipmentDocument([FromRoute] string id, [FromRoute] string documentId, CancellationToken ct = default)
    {
        var response = await shipmentClient.RemoveShipmentDocumentAsync(new RemoveShipmentDocumentRequest { ShipmentId = id, DocumentId = documentId }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("{id}/milestones")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    public async Task<IActionResult> AddShipmentMilestone([FromRoute] string id, [FromBody] MilestoneDto m, CancellationToken ct = default)
    {
        var req = new AddShipmentMilestoneRequest
        {
            ShipmentId = id,
            Status = m.Status ?? string.Empty,
            Description = m.Description ?? string.Empty,
            RecordedAt = m.RecordedAt.HasValue ? Timestamp.FromDateTimeOffset(m.RecordedAt.Value) : Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Source = m.Source ?? "MANUAL"
        };
        if (m.Latitude.HasValue)
        {
            req.Latitude = m.Latitude.Value;
        }
        if (m.Longitude.HasValue)
        {
            req.Longitude = m.Longitude.Value;
        }

        var response = await shipmentClient.AddShipmentMilestoneAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpGet("{id}/timeline")]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    public async Task<IActionResult> GetShipmentTimeline([FromRoute] string id, CancellationToken ct = default)
    {
        var response = await shipmentClient.GetShipmentTimelineAsync(new GetShipmentTimelineRequest { ShipmentId = id }, cancellationToken: ct);
        return Ok(response);
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record CreateShipmentBody(
    string? OrderId,
    string? CustomerName,
    string? OriginAddress,
    string? DestinationAddress,
    string? OriginCountry,
    string? DestinationCountry,
    List<CargoItemDto>? CargoItems);

public record UpdateShipmentBody(
    string? CustomerName,
    string? DestinationAddress,
    string? Priority,
    string? TransportMode,
    string? Notes);

public record UpdateStatusBody(string? Status, string? Note);
public record CancelShipmentBody(string? Reason);
public record ImportShipmentsBody(string? FileName, string? Content, string? ImportRequestId);

public record CargoItemDto(
    string? Name,
    int Quantity,
    decimal WeightKg,
    string? HsCode);

public record LocationDto(
    string? Type,
    string? Name,
    string? Address,
    int Sequence,
    double Latitude,
    double Longitude,
    string? ContactName,
    string? ContactPhone);

public record DocumentDto(
    string? FileName,
    string? DocumentType,
    string? StorageUrl,
    string? OcrStatus,
    double? OcrConfidence,
    string? ExtractedDataJson);

public record MilestoneDto(
    string? Status,
    string? Description,
    DateTimeOffset? RecordedAt,
    string? Source,
    double? Latitude,
    double? Longitude);
