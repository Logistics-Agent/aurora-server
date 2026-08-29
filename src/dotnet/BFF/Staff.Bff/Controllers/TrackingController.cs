using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using GpsTracking.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Giám sát GPS thời gian thực, Geofence và Cảnh báo vi phạm (GPS Tracking Service).
/// Route: /api/v1/tracking
/// </summary>
[ApiVersion("1.0")]
public class TrackingController(
    GpsTrackingService.GpsTrackingServiceClient gpsClient,
    ICurrentUserService currentUser,
    ILogger<TrackingController> logger) : StaffControllerBase
{
    [HttpGet("{id}/current")]
    [RequirePermission(PermissionConstants.Shipment.Read, "gps_tracking:read")]
    public async Task<IActionResult> GetCurrentLocation(
        [FromRoute] string id,
        [FromQuery] string type = "shipment",
        CancellationToken ct = default)
    {
        try
        {
            var req = new GetCurrentLocationRequest();
            if (string.Equals(type, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                req.VehicleId = id;
            }
            else
            {
                req.ShipmentId = id;
            }

            var response = await gpsClient.GetCurrentLocationAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("{id}/history")]
    [RequirePermission(PermissionConstants.Shipment.Read, "gps_tracking:read")]
    public async Task<IActionResult> ListPositionHistory(
        [FromRoute] string id,
        [FromQuery] string type = "shipment",
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var req = new ListPositionHistoryRequest
        {
            From = from.HasValue ? Timestamp.FromDateTimeOffset(from.Value) : null,
            To = to.HasValue ? Timestamp.FromDateTimeOffset(to.Value) : null,
            Page = page,
            PageSize = pageSize
        };

        if (string.Equals(type, "vehicle", StringComparison.OrdinalIgnoreCase))
        {
            req.VehicleId = id;
        }
        else
        {
            req.ShipmentId = id;
        }

        var response = await gpsClient.ListPositionHistoryAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("geofences")]
    [RequirePermission(PermissionConstants.Gps.GeofenceManage, "gps_tracking:create")]
    public async Task<IActionResult> CreateGeofence([FromBody] CreateGeofenceBody body, CancellationToken ct = default)
    {
        try
        {
            var req = new CreateGeofenceRequest
            {
                Name = body.Name ?? string.Empty,
                Latitude = body.Latitude,
                Longitude = body.Longitude,
                RadiusMeters = body.RadiusMeters,
                ShipmentId = body.ShipmentId ?? string.Empty,
                VehicleId = body.VehicleId ?? string.Empty
            };

            var response = await gpsClient.CreateGeofenceAsync(req, cancellationToken: ct);
            return Created($"/api/v1/tracking/geofences/{response.Id}", response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("geofences")]
    [RequirePermission(PermissionConstants.Gps.GeofenceManage, "gps_tracking:read")]
    public async Task<IActionResult> ListGeofences(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var req = new ListGeofencesRequest
        {
            IncludeInactive = includeInactive
        };

        var response = await gpsClient.ListGeofencesAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPatch("geofences/{id}/active")]
    [RequirePermission(PermissionConstants.Gps.GeofenceManage, "gps_tracking:update")]
    public async Task<IActionResult> SetGeofenceActive(
        [FromRoute] string id,
        [FromBody] SetGeofenceActiveBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new SetGeofenceActiveRequest { Id = id, IsActive = body.IsActive };
            var response = await gpsClient.SetGeofenceActiveAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("alerts")]
    [RequirePermission(PermissionConstants.Shipment.Read, "gps_tracking:read")]
    public async Task<IActionResult> ListMonitoringAlerts(
        [FromQuery] string? alertType = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var req = new ListMonitoringAlertsRequest
        {
            AlertType = alertType ?? string.Empty,
            Status = status ?? string.Empty,
            Page = page,
            PageSize = pageSize
        };

        var response = await gpsClient.ListMonitoringAlertsAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpPost("alerts/{id}/resolve")]
    [RequirePermission(PermissionConstants.Shipment.Update, "gps_tracking:update")]
    public async Task<IActionResult> ResolveMonitoringAlert(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var req = new ResolveMonitoringAlertRequest
            {
                Id = id
            };
            var response = await gpsClient.ResolveMonitoringAlertAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record CreateGeofenceBody(
    string? Name,
    double Latitude,
    double Longitude,
    double RadiusMeters,
    string? ShipmentId,
    string? VehicleId);

public record SetGeofenceActiveBody(bool IsActive);
