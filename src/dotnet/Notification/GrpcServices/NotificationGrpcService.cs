using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using Shared.Security;
using NotificationGrpc = Notification.Grpc;

namespace Notification.GrpcServices;

public sealed class NotificationGrpcService(
    NotificationDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : NotificationGrpc.NotificationService.NotificationServiceBase
{
    public override async Task<NotificationGrpc.ListNotificationsResponse> ListNotifications(
        NotificationGrpc.ListNotificationsRequest request,
        ServerCallContext context)
    {
        var userId = RequireIdentity();
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;
        if (pageSize > 100)
            throw InvalidArgument("Page size cannot exceed 100.");
        if (page > int.MaxValue / pageSize)
            throw InvalidArgument("Page number is too large.");

        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(item => item.RecipientUserId == userId);

        if (request.UnreadOnly)
            query = query.Where(item => item.ReadAt == null);

        var totalItems = await query.CountAsync(context.CancellationToken);
        var notifications = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new NotificationGrpc.ListNotificationsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
        response.Notifications.AddRange(notifications.Select(MapNotification));
        return response;
    }

    public override async Task<NotificationGrpc.NotificationResponse> MarkNotificationRead(
        NotificationGrpc.MarkNotificationReadRequest request,
        ServerCallContext context)
    {
        var userId = RequireIdentity();
        if (!Guid.TryParse(request.Id, out var notificationId))
            throw InvalidArgument("Invalid notification id.");

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                item => item.Id == notificationId && item.RecipientUserId == userId,
                context.CancellationToken);

        if (notification is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Notification was not found."));

        try
        {
            notification.MarkRead(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            throw InvalidArgument(exception.Message);
        }
        await dbContext.SaveChangesAsync(context.CancellationToken);
        return MapNotification(notification);
    }

    public override async Task<NotificationGrpc.ListNotificationPreferencesResponse> ListNotificationPreferences(
        NotificationGrpc.ListNotificationPreferencesRequest request,
        ServerCallContext context)
    {
        var userId = RequireIdentity();
        var preferences = await dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(item => item.RecipientUserId == userId)
            .OrderBy(item => item.EventType)
            .ThenBy(item => item.Channel)
            .ToListAsync(context.CancellationToken);

        var response = new NotificationGrpc.ListNotificationPreferencesResponse();
        response.Preferences.AddRange(preferences.Select(MapPreference));
        return response;
    }

    public override async Task<NotificationGrpc.NotificationPreferenceResponse> UpsertNotificationPreference(
        NotificationGrpc.UpsertNotificationPreferenceRequest request,
        ServerCallContext context)
    {
        var userId = RequireIdentity();
        var tenantId = currentUser.TenantId!.Value;
        var eventType = ParseEnum<NotificationEventType>(request.EventType, "event type");
        var channel = ParseEnum<NotificationChannel>(request.Channel, "channel");

        var preference = await dbContext.NotificationPreferences
            .SingleOrDefaultAsync(
                item => item.RecipientUserId == userId
                    && item.EventType == eventType
                    && item.Channel == channel,
                context.CancellationToken);

        try
        {
            if (preference is null)
            {
                preference = NotificationPreference.Create(
                    tenantId,
                    userId,
                    eventType,
                    channel,
                    request.IsEnabled,
                    request.RecipientAddress);
                dbContext.NotificationPreferences.Add(preference);
            }
            else
            {
                preference.Update(
                    request.IsEnabled,
                    request.RecipientAddress,
                    timeProvider.GetUtcNow());
            }
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        return MapPreference(preference);
    }

    private Guid RequireIdentity()
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty
            || currentUser.UserId is null || currentUser.UserId == Guid.Empty)
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Tenant and user context are required."));
        }

        return currentUser.UserId.Value;
    }

    private static TEnum ParseEnum<TEnum>(string value, string fieldName)
        where TEnum : struct, System.Enum
    {
        if (!System.Enum.TryParse<TEnum>(value, true, out var parsed)
            || !System.Enum.IsDefined(parsed)
            || int.TryParse(value, out _))
        {
            throw InvalidArgument($"Invalid notification {fieldName}.");
        }

        return parsed;
    }

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));

    private static NotificationGrpc.NotificationResponse MapNotification(
        NotificationMessage notification)
    {
        var response = new NotificationGrpc.NotificationResponse
        {
            Id = notification.Id.ToString(),
            ShipmentId = notification.ShipmentId?.ToString() ?? string.Empty,
            EventType = notification.EventType.ToString(),
            Channel = notification.Channel.ToString(),
            Title = notification.Title,
            Body = notification.Body,
            IsRead = notification.ReadAt is not null,
            CreatedAt = Timestamp.FromDateTimeOffset(notification.CreatedAt)
        };

        if (notification.ReadAt is not null)
            response.ReadAt = Timestamp.FromDateTimeOffset(notification.ReadAt.Value);

        return response;
    }

    private static NotificationGrpc.NotificationPreferenceResponse MapPreference(
        NotificationPreference preference) =>
        new()
        {
            Id = preference.Id.ToString(),
            EventType = preference.EventType.ToString(),
            Channel = preference.Channel.ToString(),
            IsEnabled = preference.IsEnabled,
            RecipientAddress = preference.RecipientAddress ?? string.Empty
        };
}
