using BFF.RateLimiting;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Middleware;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Logging + Kestrel hardening
builder.Host.AddBffSerilog();
builder.WebHost.AddBffKestrelLimits();

// Services
builder.Services.AddBffControllers();           // camelCase JSON
builder.Services.AddBffApiVersioning();         // /api/v{version}/... (URL segment)
builder.Services.AddCustomSwagger("Staff BFF API");
builder.Services.AddBffAuthentication(config);  // JWT từ HttpOnly cookie (Cognito)
builder.Services.AddBffCache(config);           // Redis + IPermissionCacheService
builder.Services.AddBffGrpcClients(config);     // IamService + AuthService + RoutePlanningService
builder.Services.AddBffCors(config);
builder.Services.AddBffRateLimiting(config);
builder.Services.AddBffRequestProtection();
builder.Services.AddBffHealthChecks(config);
builder.Services.AddBffOpenTelemetry(config);

var app = builder.Build();

// Pipeline — THỨ TỰ QUAN TRỌNG:
// CorrelationId → ExceptionHandling → Routing → Cors → RateLimiter → Timeouts
// → Authentication → CurrentUserContext → PermissionVersion → TenantResolution
// → SecurityHeaders → Authorization → Controllers
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCustomSwagger("Staff BFF API");
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseRequestTimeouts();

app.UseAuthentication();
app.UseMiddleware<CurrentUserContextMiddleware>();
app.UseMiddleware<PermissionVersionMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz");
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

app.Run();
