using Asp.Versioning;

namespace BuildingBlocks.BFF.Extensions;

public static class ApiVersioningExtensions
{
    /// <summary>
    /// API Versioning theo URL segment: /api/v{version}/...
    /// - Mặc định v1.0 (AssumeDefaultVersionWhenUnspecified)
    /// - ReportApiVersions: trả header api-supported-versions
    /// URL hiện tại vẫn là /api/v1/... nên YARP gateway KHÔNG cần đổi route match.
    /// </summary>
    public static IServiceCollection AddBffApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new ApiVersion(1, 0);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ReportApiVersions = true;
            o.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(o =>
        {
            o.GroupNameFormat = "'v'VVV";           // → "v1"
            o.SubstituteApiVersionInUrl = true;      // /api/v{version}/... → /api/v1/...
        });

        return services;
    }
}
