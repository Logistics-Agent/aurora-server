using GpsTracking.Application.Consumers;
using GpsTracking.Application.Ingestion;
using GpsTracking.Application.Monitoring;
using GpsTracking.Application.Queries;
using GpsTracking.Application.Shipments;
using GpsTracking.GrpcServices;
using GpsTracking.Infrastructure.BackgroundJobs;
using GpsTracking.Infrastructure.Persistences;
using MassTransit;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<GpsTrackingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("GpsTracking")));

builder.Services.AddSharedMassTransit(
    builder.Configuration,
    bus => bus.AddConsumer<ShipmentTrackingConsumer>());

builder.Services.AddSingleton(TimeProvider.System);
var monitoringOptions = builder.Configuration
    .GetSection(MonitoringOptions.SectionName)
    .Get<MonitoringOptions>() ?? new MonitoringOptions();
monitoringOptions.Validate();
builder.Services.AddSingleton(monitoringOptions);

builder.Services.AddScoped<IPositionIngestionService, PositionIngestionService>();
builder.Services.AddScoped<ILocationQueryService, LocationQueryService>();
builder.Services.AddScoped<IPositionMonitoringService, PositionMonitoringService>();
builder.Services.AddScoped<IMonitoringManagementService, MonitoringManagementService>();
builder.Services.AddScoped<SignalLossMonitor>();
builder.Services.AddScoped<IShipmentAssignmentProjector, ShipmentAssignmentProjector>();
builder.Services.AddHostedService<SignalLossMonitoringBackgroundService>();

builder.Services.AddOptions<GpsOutboxPublisherOptions>()
    .Bind(builder.Configuration.GetSection(GpsOutboxPublisherOptions.SectionName))
    .Validate(options => options.BatchSize is >= 1 and <= 1_000,
        "GpsOutbox:BatchSize must be between 1 and 1000.")
    .Validate(options => options.MaxRetries is >= 1 and <= 100,
        "GpsOutbox:MaxRetries must be between 1 and 100.")
    .Validate(options => options.PollingInterval > TimeSpan.Zero,
        "GpsOutbox:PollingInterval must be positive.")
    .ValidateOnStart();
builder.Services.AddScoped<IGpsOutboxBatchStore, GpsOutboxBatchStore>();
builder.Services.AddScoped<IGpsIntegrationEventPublisher, GpsIntegrationEventPublisher>();
builder.Services.AddScoped<GpsOutboxProcessor>();
builder.Services.AddHostedService<GpsOutboxPublisherBackgroundService>();

var app = builder.Build();

app.MapGrpcService<GpsTrackingGrpcService>();
app.MapGet("/", () => "GPS Tracking gRPC Service");

app.Run();
