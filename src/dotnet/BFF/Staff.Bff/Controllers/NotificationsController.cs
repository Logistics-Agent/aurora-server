using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Notification.Grpc;
using Google.Protobuf.WellKnownTypes;
using Shared.Constants;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý thông báo người dùng và cấu hình kênh nhận tin (Notification Service).
/// Route: /api/v1/notifications
/// </summary>
[ApiVersion("1.0")]
[Authorize]
[RequirePermission(PermissionConstants.Notification.Access)]
public class NotificationsController(
    NotificationService.NotificationServiceClient notifClient) : StaffControllerBase
{
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceBody body, CancellationToken ct = default)
    {
        var response = await notifClient.RegisterDeviceAsync(new RegisterDeviceRequest
        {
            Token = body.Token ?? string.Empty,
            Platform = body.Platform ?? string.Empty,
            AppVersion = body.AppVersion ?? string.Empty
        }, cancellationToken: ct);
        return Ok(response);
    }

    [HttpDelete("devices/{id}")]
    public async Task<IActionResult> RemoveDevice([FromRoute] string id, CancellationToken ct = default)
    {
        await notifClient.RemoveDeviceAsync(new RemoveDeviceRequest { Id = id }, cancellationToken: ct);
        return NoContent();
    }

    [HttpPost("subscriptions/shipments/{shipmentId}")]
    public async Task<IActionResult> SubscribeShipment([FromRoute] string shipmentId, CancellationToken ct = default)
    {
        await notifClient.SubscribeShipmentAsync(new SubscribeShipmentRequest { ShipmentId = shipmentId }, cancellationToken: ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> ListNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var req = new ListNotificationsRequest
        {
            Page = page,
            PageSize = pageSize,
            UnreadOnly = unreadOnly
        };

        var response = await notifClient.ListNotificationsAsync(req, cancellationToken: ct);
        return Ok(response);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default) =>
        Ok(await notifClient.GetUnreadCountAsync(new GetUnreadCountRequest(), cancellationToken: ct));

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var req = new MarkNotificationReadRequest { Id = id };
            await notifClient.MarkNotificationReadAsync(req, cancellationToken: ct);
            return NoContent();
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct = default)
    {
        var response = await notifClient.MarkAllNotificationsReadAsync(
            new MarkAllNotificationsReadRequest(), cancellationToken: ct);
        return Ok(response);
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record RegisterDeviceBody(string? Token, string? Platform, string? AppVersion);
