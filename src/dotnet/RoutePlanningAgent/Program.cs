using System;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.GrpcServices;
using RoutePlanningAgent.Infrastructure.AI;
using RoutePlanningAgent.Infrastructure.Consumers;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Rules;
using RoutePlanningAgent.Infrastructure.Rules.Rules;
using RoutePlanningAgent.Infrastructure.Services;
using Shared.Extensions;
using Shared.Interceptors;
using Shared.Rules;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// 1. Shared Services (CurrentUserService, Redis, Interceptors)
builder.Services.AddSharedServices(builder.Configuration);

// 2. MassTransit with Consumers
builder.Services.AddSharedMassTransit(builder.Configuration, x =>
{
    x.AddConsumer<TenantAiConfigChangedConsumer>();
    x.AddConsumer<TenantRuleConfigChangedConsumer>();
});

// 3. gRPC with Auth and Exception Interceptors
builder.Services.AddGrpc(opts =>
{
    opts.Interceptors.Add<AuthInterceptor>();
    opts.Interceptors.Add<ExceptionInterceptor>();
});

// 4. MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// 5. EF Core — PostgreSQL (Database per Service)
builder.Services.AddDbContext<RoutePlanningDbContext>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("RoutePlanningAgent")));

// 6. Business Rule Engine & Rules
builder.Services.AddScoped<IRule<RouteRuleContext>, HeavyWeightRule>();
builder.Services.AddScoped<IRule<RouteRuleContext>, LargeVolumeRule>();
builder.Services.AddScoped<IRule<RouteRuleContext>, RouteStopCountRule>();
builder.Services.AddScoped<IRule<RouteRuleContext>, OnDemandTypeRule>();
builder.Services.AddScoped<IRule<RouteRuleContext>, LongDurationRule>();
builder.Services.AddScoped<IRule<RouteRuleContext>, MinimumStopsRule>();
builder.Services.AddScoped<IRule<RouteRuleContext>, MultiHubRule>();
builder.Services.AddScoped<IRouteRuleEngine, RouteRuleEngine>();

// 7. AI Service & Semantic Kernel Pool registration (from Shared extension)
builder.Services.AddSemanticKernel(builder.Configuration);
builder.Services.AddScoped<IRouteAiService, RouteAiService>();

// 8. Services Layer
builder.Services.AddScoped<ITenantAiConfigService, TenantAiConfigService>();
builder.Services.AddScoped<ITenantRuleConfigService, TenantRuleConfigService>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();

// 9. gRPC client calling Regulatory Compliance RAG Service
builder.Services.AddGrpcClient<RegulatoryComplianceRag.Grpc.ComplianceRag.ComplianceRagClient>(o =>
{
    var serviceUrl = builder.Configuration["ComplianceRag:ServiceUrl"] ?? "http://localhost:5001";
    o.Address = new Uri(serviceUrl);
});
builder.Services.AddScoped<IComplianceRagService, ComplianceRagClient>();

var app = builder.Build();

// 10. Map gRPC Endpoints
app.MapGrpcService<RoutePlanningGrpcService>();
app.MapGet("/", () => "Route Planning Agent Service is running.");

app.Run();
