using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Middleware;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
// builder.Services.AddCustomSwagger("Staff BFF API");

builder.Services.AddBffAuthentication(config);
// builder.Services.AddBffGrpcClients(config);

var app = builder.Build();

// app.UseCustomSwagger("Staff BFF API");
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<TokenRefreshMiddleware>();
app.UseMiddleware<CurrentUserContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
