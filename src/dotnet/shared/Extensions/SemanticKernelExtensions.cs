using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.AI;

namespace Shared.Extensions;

public static class SemanticKernelExtensions
{
    public static IServiceCollection AddSemanticKernel(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RoutePlanningOptions>(configuration.GetSection("RoutePlanning"));

        // Register pools as keyed singletons to support multiple IApiKeyPool<string> registrations
        services.AddKeyedSingleton<IApiKeyPool<string>>("Gemini", (sp, key) =>
        {
            var opts = sp.GetRequiredService<IOptions<RoutePlanningOptions>>().Value;
            return new RoundRobinApiKeyPool<string>(opts.KeyPool.GeminiApiKeys);
        });

        services.AddSingleton<IApiKeyPool<AzureOpenAiKeyEntry>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RoutePlanningOptions>>().Value;
            return new RoundRobinApiKeyPool<AzureOpenAiKeyEntry>(opts.KeyPool.AzureOpenAiKeys);
        });

        return services;
    }
}
