using Notification.Domain.Enums;

namespace Notification.Application.DTOs;

public sealed record RegisterDeviceRequest(string Token, DevicePlatform Platform, string? AppVersion);
public sealed record DeviceResponse(Guid DeviceId, DevicePlatform Platform, bool IsActive);
public sealed record NotificationResponse(Guid Id, string Type, string Title, string Body, Guid? ShipmentId, string? ShipmentNumber, string? ActionUrl, bool IsRead, DateTimeOffset CreatedAt);
public sealed record PagedNotifications(IReadOnlyCollection<NotificationResponse> Items, int Page, int PageSize, int TotalCount);
public sealed record RegisterSubscriptionRequest(Guid ShipmentId);
