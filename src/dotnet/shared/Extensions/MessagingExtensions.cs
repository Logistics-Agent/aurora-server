using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Cache;
using Shared.Interceptors;
using Shared.Security;

namespace Shared.Extensions;

public static class SharedServiceExtensions
{
    /// <summary>
    /// Đăng ký tất cả shared services: CurrentUserService, Redis, Interceptors.
    /// Gọi một lần trong Program.cs của mỗi service.
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DevelopmentIdentityOptions>()
            .Bind(configuration.GetSection(DevelopmentIdentityOptions.SectionName))
            .Validate(
                DevelopmentIdentityOptions.IsValid,
                "Enabled DevelopmentIdentity requires non-empty UserId, TenantId, a positive PermissionVersion, and valid roles and permissions.")
            .ValidateOnStart();

        // Security context — Scoped để mỗi request có instance riêng
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

        // Redis Permission Cache
        var redisConn = BuildRedisConnectionString(configuration);
        services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);
        services.AddScoped<IPermissionCacheService, PermissionCacheService>();

        // gRPC Interceptors
        services.AddScoped<AuthInterceptor>();
        services.AddTransient<ClientMetadataInterceptor>();

        // Audit EF Core Interceptor
        services.AddScoped<AuditSaveChangesInterceptor>();

        return services;
    }

    /// <summary>
    /// Cấu hình MassTransit + RabbitMQ với Raw JSON cho cross-platform interop (NestJS).
    /// Snake-case exchange names via [EntityName] attribute trên Event records.
    /// </summary>
    public static IServiceCollection AddSharedMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.AddMassTransit(x =>
        {
            configureBus?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    configuration["RabbitMQ:Host"] ?? "localhost",
                    "/",
                    h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });

                // Bắt buộc để NestJS (và bất kỳ service non-.NET nào) đọc được message
                cfg.UseRawJsonSerializer();

                // Retry theo cấp số nhân cho mọi consumer: 5 lần, 1s → 30s
                cfg.UseMessageRetry(r => r.Exponential(
                    5,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(2)));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
    public static string BuildRedisConnectionString(IConfiguration configuration)
    {
        var host = configuration["Redis:Host"]
            ?? configuration["Redis:ConnectionString"]
            ?? configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        var password = configuration["Redis:Password"];
        var ssl = configuration.GetValue<bool?>("Redis:Ssl") ?? configuration.GetValue<bool?>("Redis:UseSsl") ?? (!string.IsNullOrWhiteSpace(password));
        var abortConnect = configuration.GetValue<bool?>("Redis:AbortConnect") ?? false;

        if (string.IsNullOrWhiteSpace(password))
        {
            if (host.Contains(','))
                return host;

            var parts = new List<string> { host };
            if (ssl) parts.Add("ssl=true");
            if (!abortConnect) parts.Add("abortConnect=false");
            return string.Join(",", parts);
        }

        // Redis Cloud / Azure Redis: host:port,password=xxx,ssl=true/false,abortConnect=false
        return $"{host},password={password},ssl={ssl.ToString().ToLowerInvariant()},abortConnect={abortConnect.ToString().ToLowerInvariant()}";
    }
}
