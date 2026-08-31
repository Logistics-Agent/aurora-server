using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Firebase;
using Notification.Infrastructure.BackgroundJobs;
using Notification.Infrastructure.Persistences;
using Shared.Extensions;
using Shared.Security;
using Shared.Interceptors;
using Notification.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<NotificationServiceAuthInterceptor>();
    options.Interceptors.Add<AuthInterceptor>();
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var authority = builder.Configuration["Auth:Jwt:Authority"];
    if (!string.IsNullOrWhiteSpace(authority)) options.Authority = authority;
    options.Audience = builder.Configuration["Auth:Jwt:Audience"];
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
});
builder.Services.AddAuthorization();
builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<FirebaseConfigurationHealthCheck>("firebase-configuration", tags: ["ready"]);
builder.Services.AddOptions<NotificationServiceAuthOptions>()
    .Bind(builder.Configuration.GetSection(NotificationServiceAuthOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.AllowedServiceId), "ServiceAuth:AllowedServiceId is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "ServiceAuth:ApiKey is required.")
    .ValidateOnStart();
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("NotificationDatabase"),
    npgsql => npgsql.MigrationsAssembly(typeof(Program).Assembly.FullName)));
builder.Services.AddOptions<FirebaseOptions>()
    .Bind(builder.Configuration.GetSection(FirebaseOptions.SectionName))
    .Validate(options => !options.Enabled || options.HasInlineCredentials || !string.IsNullOrWhiteSpace(options.CredentialsPath),
        "Firebase credentials are required when Firebase:Enabled is true.")
    .ValidateOnStart();
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
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentSubmittedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentPickedUpConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ShipmentCompletedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.DocumentAttachedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.GpsMonitoringAlertConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.DocumentOcrCompletedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.DocumentOcrFailedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ComplianceEvaluationCompletedConsumer>();
    x.AddConsumer<Notification.Infrastructure.Messaging.Consumers.ComplianceEvaluationFailedConsumer>();
});
builder.Services.AddRouting();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<NotificationCurrentUserMiddleware>();
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapGrpcService<Notification.GrpcServices.NotificationGrpcService>();
app.MapGet("/", () => "Notification gRPC Service");

app.Run();

public partial class Program { }
