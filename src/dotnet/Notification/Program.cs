using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Notification.Application.DTOs;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Firebase;
using Notification.Infrastructure.BackgroundJobs;
using Notification.Infrastructure.Persistences;
using Shared.Extensions;
using Shared.Security;
using Notification.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var authority = builder.Configuration["Auth:Jwt:Authority"];
    if (!string.IsNullOrWhiteSpace(authority)) options.Authority = authority;
    options.Audience = builder.Configuration["Auth:Jwt:Audience"];
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
});
builder.Services.AddAuthorization();
builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("NotificationDatabase"),
    npgsql => npgsql.MigrationsAssembly(typeof(Program).Assembly.FullName)));
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.AddSingleton<IFcmPushProvider, FirebasePushProvider>();
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddScoped<IRecipientResolver, Notification.Infrastructure.Messaging.SubscriptionRecipientResolver>();
builder.Services.AddScoped<Notification.Infrastructure.Messaging.NotificationEventProcessor>();
builder.Services.AddHostedService<NotificationDeliveryWorker>();
builder.Services.AddHostedService<FirebaseAdminInitializer>();
builder.Services.AddSharedMassTransit(builder.Configuration, x =>
{
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentStatusChangedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentCancelledConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentDeliveredConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentCreatedConsumer>();
});
builder.Services.AddRouting();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<NotificationCurrentUserMiddleware>();
app.MapHealthChecks("/health");

app.MapPost("/api/v1/notification-devices", async (RegisterDeviceRequest request, ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096) return Results.BadRequest(new { error = "Invalid FCM token." });
    var device = await db.Devices.SingleOrDefaultAsync(x => x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId && x.FcmToken == request.Token, ct);
    if (device is null) db.Devices.Add(device = NotificationDevice.Register(currentUser.TenantId.Value, currentUser.UserId.Value, request.Token, request.Platform));
    else device.Touch(request.Token, request.Platform);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new DeviceResponse(device.Id, device.Platform, device.IsActive));
});

app.MapDelete("/api/v1/notification-devices/{id:guid}", async (Guid id, ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId, ct);
    if (device is null) return Results.NotFound();
    device.Deactivate(); await db.SaveChangesAsync(ct); return Results.NoContent();
});

app.MapPost("/api/v1/notification-subscriptions", async (RegisterSubscriptionRequest request, ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    if (request.ShipmentId == Guid.Empty) return Results.BadRequest(new { error = "ShipmentId is required." });
    if (!await db.Subscriptions.AnyAsync(x => x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId && x.ShipmentId == request.ShipmentId, ct))
        db.Subscriptions.Add(NotificationSubscription.Create(currentUser.TenantId.Value, currentUser.UserId.Value, request.ShipmentId));
    await db.SaveChangesAsync(ct); return Results.NoContent();
});

app.MapGet("/api/v1/notifications", async (int? page, int? pageSize, ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    var p = Math.Max(page ?? 1, 1); var size = Math.Clamp(pageSize ?? 20, 1, 100);
    var query = db.Notifications.Where(x => x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
    var total = await query.CountAsync(ct); var items = await query.Skip((p - 1) * size).Take(size).Select(x => new NotificationResponse(x.Id, x.Type, x.Title, x.Body, x.ShipmentId, x.ShipmentNumber, x.ActionUrl, x.Status == Notification.Domain.Enums.NotificationStatus.Read, x.CreatedAt)).ToListAsync(ct);
    return Results.Ok(new PagedNotifications(items, p, size, total));
});

app.MapGet("/api/v1/notifications/unread-count", async (ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    var count = await db.Notifications.CountAsync(x => x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId && x.Status != Notification.Domain.Enums.NotificationStatus.Read, ct);
    return Results.Ok(new { unreadCount = count });
});

app.MapPut("/api/v1/notifications/{id:guid}/read", async (Guid id, ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId, ct);
    if (notification is null) return Results.NotFound(); notification.MarkRead(); await db.SaveChangesAsync(ct); return Results.NoContent();
});

app.MapPut("/api/v1/notifications/read-all", async (ICurrentUserService currentUser, NotificationDbContext db, CancellationToken ct) =>
{
    if (!currentUser.UserId.HasValue || !currentUser.TenantId.HasValue) return Results.Unauthorized();
    var notifications = await db.Notifications.Where(x => x.TenantId == currentUser.TenantId && x.UserId == currentUser.UserId && x.Status != Notification.Domain.Enums.NotificationStatus.Read).ToListAsync(ct);
    foreach (var notification in notifications) notification.MarkRead();
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { updatedCount = notifications.Count });
});

app.Run();

public partial class Program { }
