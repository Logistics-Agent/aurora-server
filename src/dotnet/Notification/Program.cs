using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Consumers;
using Notification.Application.Delivery;
using Notification.Application.Services;
using Notification.GrpcServices;
using Notification.Infrastructure.BackgroundJobs;
using Notification.Infrastructure.Persistences;
using Notification.Infrastructure.Providers;
using Shared.Extensions;
using Shared.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>();
    options.Interceptors.Add<ExceptionInterceptor>();
});

builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddTransient<ExceptionInterceptor>();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("Notification")));

builder.Services.AddSharedMassTransit(
    builder.Configuration,
    bus => bus.AddConsumer<ShipmentNotificationConsumer>());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IShipmentNotificationProjector, ShipmentNotificationProjector>();

builder.Services.Configure<SmtpEmailOptions>(
    builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddScoped<IEmailNotificationProvider, SmtpEmailNotificationProvider>();
builder.Services.AddScoped<IInAppNotificationProvider, InAppNotificationProvider>();
builder.Services.AddScoped<INotificationDeliveryProvider>(
    services => services.GetRequiredService<IEmailNotificationProvider>());
builder.Services.AddScoped<INotificationDeliveryProvider>(
    services => services.GetRequiredService<IInAppNotificationProvider>());

var retryOptions = builder.Configuration
    .GetSection("NotificationRetry")
    .Get<NotificationRetryOptions>() ?? new NotificationRetryOptions();
builder.Services.AddSingleton(retryOptions);
builder.Services.AddSingleton<INotificationRetryPolicy, NotificationRetryPolicy>();
builder.Services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();

builder.Services.Configure<NotificationDeliveryWorkerOptions>(
    builder.Configuration.GetSection("NotificationDelivery"));
builder.Services.AddHostedService<NotificationDeliveryWorker>();

var app = builder.Build();

app.MapGrpcService<NotificationGrpcService>();
app.MapGet("/", () => "Notification gRPC Service");

app.Run();
