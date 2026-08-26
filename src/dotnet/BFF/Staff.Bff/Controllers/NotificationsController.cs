using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Notification.Grpc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý thông báo người dùng và cấu hình kênh nhận tin (Notification Service).
/// Route: /api/v1/notifications
/// </summary>
[ApiVersion("1.0")]
public class NotificationsController(
    NotificationService.NotificationServiceClient notifClient,
    ICurrentUserService currentUser,
    ILogger<NotificationsController> logger) : StaffControllerBase
{
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

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var req = new MarkNotificationReadRequest { Id = id };
            var response = await notifClient.MarkNotificationReadAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> ListNotificationPreferences(CancellationToken ct = default)
    {
        var response = await notifClient.ListNotificationPreferencesAsync(
            new ListNotificationPreferencesRequest(), cancellationToken: ct);
        return Ok(response);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpsertNotificationPreference(
        [FromBody] UpsertNotificationPreferenceBody body,
        CancellationToken ct = default)
    {
        var req = new UpsertNotificationPreferenceRequest
        {
            EventType = body.EventType ?? string.Empty,
            Channel = body.Channel ?? string.Empty,
            IsEnabled = body.IsEnabled,
            RecipientAddress = body.RecipientAddress ?? string.Empty
        };

        var response = await notifClient.UpsertNotificationPreferenceAsync(req, cancellationToken: ct);
        return Ok(response);
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record UpsertNotificationPreferenceBody(
    string? EventType,
    string? Channel,
    bool IsEnabled,
    string? RecipientAddress);
