namespace BuildingBlocks.BFF.Extensions;

public static class CorsExtensions
{
    /// <summary>
    /// Đăng ký CORS policies cho BFF.
    /// Cho phép các origins được định nghĩa trong cấu hình truy cập.
    /// Hỗ trợ credentials (để gửi/nhận HttpOnly Cookies).
    /// </summary>
    public static IServiceCollection AddBffCors(
        this IServiceCollection services,
        IConfiguration config)
    {
        var configuredOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var defaultOrigins = new[]
        {
            "http://localhost:3000",
            "http://localhost:3001",
            "http://localhost:3002",
            "http://localhost:5173",
            "http://127.0.0.1:3000",
            "http://127.0.0.1:3001",
            "http://127.0.0.1:3002",
            "http://127.0.0.1:5173",
            "https://admin.humanak.cyou",
            "https://system.humanak.cyou",
            "https://humanak.cyou",
            "https://api.humanak.cyou"
        };

        var allOrigins = new HashSet<string>(configuredOrigins.Concat(defaultOrigins), StringComparer.OrdinalIgnoreCase);

        services.AddCors(opts =>
        {
            opts.AddDefaultPolicy(policy =>
                policy.SetIsOriginAllowed(origin =>
                      {
                          if (string.IsNullOrWhiteSpace(origin)) return false;
                          if (allOrigins.Contains(origin)) return true;
                          if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                          {
                              var host = uri.Host.ToLowerInvariant();
                              return host == "localhost"
                                  || host == "127.0.0.1"
                                  || host == "humanak.cyou"
                                  || host.EndsWith(".humanak.cyou", StringComparison.OrdinalIgnoreCase);
                          }
                          return false;
                      })
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()); // Bắt buộc phải có để dùng HttpOnly Cookie
        });

        return services;
    }
}
