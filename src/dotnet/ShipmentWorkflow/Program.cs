using Shared.Extensions;
using Shared.Interceptors;
using ShipmentWorkflow.GrpcServices;
using Microsoft.EntityFrameworkCore;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentWorkflow.Application.Interfaces;
using ShipmentWorkflow.Infrastructure.Services;
using ShipmentWorkflow.Infrastructure.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>();
    options.Interceptors.Add<ExceptionInterceptor>();
});

builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddTransient<ExceptionInterceptor>();
builder.Services.AddSharedMassTransit(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddDbContext<ShipmentWorkflowDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
            npgsql.MigrationsAssembly("ShipmentWorkflow")));

builder.Services.AddScoped<
    IShipmentNumberGenerator,
    ShipmentNumberGenerator>();

builder.Services.AddOptions<ShipmentOutboxPublisherOptions>()
    .Bind(builder.Configuration.GetSection(ShipmentOutboxPublisherOptions.SectionName))
    .Validate(options => options.BatchSize > 0, "ShipmentOutbox:BatchSize must be positive.")
    .Validate(options => options.MaxRetries > 0, "ShipmentOutbox:MaxRetries must be positive.")
    .Validate(
        options => options.PollingInterval > TimeSpan.Zero,
        "ShipmentOutbox:PollingInterval must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<
    IShipmentIntegrationEventPublisher,
    ShipmentIntegrationEventPublisher>();
builder.Services.AddScoped<ShipmentOutboxProcessor>();
builder.Services.AddHostedService<ShipmentOutboxPublisherBackgroundService>();

var app = builder.Build();

app.MapGrpcService<ShipmentGrpcService>();

app.MapGet("/", () => "Shipment Workflow gRPC Service");

app.Run();
