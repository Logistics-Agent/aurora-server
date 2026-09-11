using Amazon.CognitoIdentityProvider;
using IamTenant.Application.Interfaces;
using IamTenant.GrpcServices;
using IamTenant.Infrastructure.Auth.Cognito;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;
using Shared.Interceptors;

var builder = WebApplication.CreateBuilder(args);

// ── gRPC với AuthInterceptor từ Shared ───────────────────────────────────────
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>(); // Populate ICurrentUserService từ metadata
    options.Interceptors.Add<ExceptionInterceptor>(); // Global Exception Handling
});

// ── Shared Services: CurrentUserService, Redis, Interceptors, MassTransit ────
builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddSharedMassTransit(builder.Configuration);

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── Background Jobs ───────────────────────────────────────────────────────────
builder.Services.AddHostedService<IamTenant.Infrastructure.BackgroundJobs.OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<IamTenant.Infrastructure.BackgroundJobs.SoftDeleteCleanupWorker>();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();

// ── AWS Cognito ───────────────────────────────────────────────────────────────
builder.Services.Configure<CognitoOptions>(options =>
{
    builder.Configuration.GetSection("Cognito").Bind(options);

    if (string.IsNullOrWhiteSpace(options.ClientId))
    {
        options.ClientId = builder.Configuration["AWS_COGNITO_CLIENT_ID"]
            ?? builder.Configuration["AWS_COGNITO_APP_CLIENT_ID"]
            ?? builder.Configuration["COGNITO_APP_CLIENT_ID"]
            ?? builder.Configuration["COGNITO_CLIENT_ID"]
            ?? builder.Configuration["Cognito__ClientId"]
            ?? builder.Configuration["Cognito:ClientId"]
            ?? string.Empty;
    }
    if (string.IsNullOrWhiteSpace(options.UserPoolId))
    {
        options.UserPoolId = builder.Configuration["AWS_COGNITO_USER_POOL_ID"]
            ?? builder.Configuration["COGNITO_USER_POOL_ID"]
            ?? builder.Configuration["Cognito__UserPoolId"]
            ?? builder.Configuration["Cognito:UserPoolId"]
            ?? string.Empty;
    }
    if (string.IsNullOrWhiteSpace(options.ClientSecret))
    {
        options.ClientSecret = builder.Configuration["AWS_COGNITO_CLIENT_SECRET"]
            ?? builder.Configuration["AWS_COGNITO_APP_CLIENT_SECRET"]
            ?? builder.Configuration["COGNITO_APP_CLIENT_SECRET"]
            ?? builder.Configuration["Cognito__ClientSecret"]
            ?? string.Empty;
    }
});
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();

// ── PostgreSQL — Database per Service ────────────────────────────────────────
builder.Services.AddDbContext<IamTenantDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("IamTenant")));

var app = builder.Build();

// ── gRPC Endpoints ────────────────────────────────────────────────────────────
app.MapGrpcService<IamGrpcService>();
app.MapGrpcService<AuthGrpcService>();

app.MapGet("/", () => "IAM Tenant gRPC Service — use a gRPC client to connect.");
app.MapHealthChecks("/healthz");
app.Run();
