using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Grpc;
using Notification.Infrastructure.Persistences;
using Shared.Security;

namespace Notification.GrpcServices;

public sealed class NotificationGrpcService(
    NotificationDbContext db,
    ICurrentUserService currentUser) : NotificationService.NotificationServiceBase
{
    public override async Task<DeviceResponse> RegisterDevice(
        RegisterDeviceRequest request,
        ServerCallContext context)
    {
        var identity = RequireIdentity();
        if (!System.Enum.TryParse<DevicePlatform>(request.Platform, true, out var platform) ||
            !System.Enum.IsDefined(platform))
            throw Invalid("Invalid device platform.");

        var device = await db.DevicesFor(identity.TenantId, identity.UserId)
            .SingleOrDefaultAsync(x => x.FcmToken == request.Token, context.CancellationToken);
        if (device is null)
        {
            try
            {
                device = NotificationDevice.Register(identity.TenantId, identity.UserId, request.Token, platform);
            }
            catch (ArgumentException ex)
            {
                throw Invalid(ex.Message);
            }
            db.Devices.Add(device);
        }
        else
        {
            try
            {
                device.Touch(request.Token, platform);
            }
            catch (ArgumentException ex)
            {
                throw Invalid(ex.Message);
            }
        }

        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, "The FCM token is already active for another user."));
        }
        return new DeviceResponse { Id = device.Id.ToString(), Platform = device.Platform.ToString(), IsActive = device.IsActive };
    }

    public override async Task<Empty> RemoveDevice(RemoveDeviceRequest request, ServerCallContext context)
    {
        var identity = RequireIdentity();
        var id = ParseGuid(request.Id, "Invalid device id.");
        var device = await db.DevicesFor(identity.TenantId, identity.UserId)
            .SingleOrDefaultAsync(x => x.Id == id, context.CancellationToken)
            ?? throw NotFound("Device not found.");
        device.Deactivate();
        await db.SaveChangesAsync(context.CancellationToken);
        return new Empty();
    }

    public override async Task<Empty> SubscribeShipment(SubscribeShipmentRequest request, ServerCallContext context)
    {
        var identity = RequireIdentity();
        var shipmentId = ParseGuid(request.ShipmentId, "Invalid shipment id.");
        if (!await db.SubscriptionsFor(identity.TenantId, identity.UserId)
                .AnyAsync(x => x.ShipmentId == shipmentId, context.CancellationToken))
        {
            db.Subscriptions.Add(NotificationSubscription.Create(identity.TenantId, identity.UserId, shipmentId));
            try
            {
                await db.SaveChangesAsync(context.CancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // A concurrent request may have created the same subscription.
                if (!await db.SubscriptionsFor(identity.TenantId, identity.UserId)
                        .AnyAsync(x => x.ShipmentId == shipmentId, context.CancellationToken))
                    throw new RpcException(new Status(StatusCode.AlreadyExists, "Shipment subscription already exists."));
            }
        }
        return new Empty();
    }

    public override async Task<ListNotificationsResponse> ListNotifications(
        ListNotificationsRequest request,
        ServerCallContext context)
    {
        var identity = RequireIdentity();
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize == 0 ? 20 : request.PageSize, 1, 100);
        var query = db.NotificationsFor(identity.TenantId, identity.UserId);
        if (request.UnreadOnly) query = query.Where(x => x.Status != NotificationStatus.Read);

        var total = await query.CountAsync(context.CancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListNotificationsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
        response.Notifications.AddRange(items.Select(MapNotification));
        return response;
    }

    public override async Task<UnreadCountResponse> GetUnreadCount(
        GetUnreadCountRequest request,
        ServerCallContext context)
    {
        var identity = RequireIdentity();
        var count = await db.NotificationsFor(identity.TenantId, identity.UserId)
            .CountAsync(x => x.Status != NotificationStatus.Read, context.CancellationToken);
        return new UnreadCountResponse { Count = count };
    }

    public override async Task<Empty> MarkNotificationRead(
        MarkNotificationReadRequest request,
        ServerCallContext context)
    {
        var identity = RequireIdentity();
        var id = ParseGuid(request.Id, "Invalid notification id.");
        var notification = await db.NotificationsFor(identity.TenantId, identity.UserId)
            .SingleOrDefaultAsync(x => x.Id == id, context.CancellationToken)
            ?? throw NotFound("Notification not found.");
        notification.MarkRead();
        await db.SaveChangesAsync(context.CancellationToken);
        return new Empty();
    }

    public override async Task<CountResponse> MarkAllNotificationsRead(
        MarkAllNotificationsReadRequest request,
        ServerCallContext context)
    {
        var identity = RequireIdentity();
        var notifications = await db.NotificationsFor(identity.TenantId, identity.UserId)
            .Where(x => x.Status != NotificationStatus.Read)
            .ToListAsync(context.CancellationToken);
        foreach (var notification in notifications) notification.MarkRead();
        await db.SaveChangesAsync(context.CancellationToken);
        return new CountResponse { Count = notifications.Count };
    }

    private (Guid TenantId, Guid UserId) RequireIdentity()
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue ||
            currentUser.TenantId.Value == Guid.Empty || currentUser.UserId.Value == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authenticated user and tenant are required."));
        }
        return (currentUser.TenantId.Value, currentUser.UserId.Value);
    }

    private static NotificationResponse MapNotification(Notification.Domain.Entities.Notification notification) => new()
    {
        Id = notification.Id.ToString(),
        ShipmentId = notification.ShipmentId?.ToString() ?? string.Empty,
        ShipmentNumber = notification.ShipmentNumber ?? string.Empty,
        EventType = notification.Type,
        Channel = notification.Channel.ToString(),
        Title = notification.Title,
        Body = notification.Body,
        ActionUrl = notification.ActionUrl ?? string.Empty,
        IsRead = notification.Status == NotificationStatus.Read,
        CreatedAt = Timestamp.FromDateTimeOffset(notification.CreatedAt),
        ReadAt = notification.ReadAt.HasValue ? Timestamp.FromDateTimeOffset(notification.ReadAt.Value) : null
    };

    private static Guid ParseGuid(string value, string message) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw Invalid(message);

    private static RpcException Invalid(string message) => new(new Status(StatusCode.InvalidArgument, message));
    private static RpcException NotFound(string message) => new(new Status(StatusCode.NotFound, message));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
