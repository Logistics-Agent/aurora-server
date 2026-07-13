using Shared.Extensions;
using Shared.Interceptors;
using ShipmentWorkflow.GrpcServices;
using Microsoft.EntityFrameworkCore;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentWorkflow.Application.Interfaces;
using ShipmentWorkflow.Infrastructure.Services;

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

var app = builder.Build();

app.MapGrpcService<ShipmentGrpcService>();

app.MapGet("/", () => "Shipment Workflow gRPC Service");

app.Run();
