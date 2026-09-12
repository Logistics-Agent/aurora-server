using DocumentOcr.Application.Uploads;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentOcr.Tests;

public sealed class DocumentUploadBridgeCorsPolicyTests
{
    [Fact]
    public async Task PreflightAllowsConfiguredOriginAndOnlyTheUploadMethodAndHeader()
    {
        using var provider = CreateProvider("https://portal.example");
        var context = Preflight("https://portal.example");

        var policy = await provider.GetRequiredService<ICorsPolicyProvider>().GetPolicyAsync(
            context, DocumentUploadBridgeCorsPolicy.Name);
        var result = provider.GetRequiredService<ICorsService>().EvaluatePolicy(context, policy!);

        Assert.True(result.IsOriginAllowed);
        Assert.True(result.IsPreflightRequest);
        Assert.Contains("PUT", result.AllowedMethods);
        Assert.Contains("OPTIONS", result.AllowedMethods);
        Assert.DoesNotContain("POST", result.AllowedMethods);
        Assert.Contains("content-type", result.AllowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", result.AllowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.False(result.SupportsCredentials);
    }

    [Fact]
    public async Task PreflightDeniesAnOriginOutsideTheConfiguredAllowlist()
    {
        using var provider = CreateProvider("https://portal.example");
        var context = Preflight("https://attacker.example");

        var policy = await provider.GetRequiredService<ICorsPolicyProvider>().GetPolicyAsync(
            context, DocumentUploadBridgeCorsPolicy.Name);
        var result = provider.GetRequiredService<ICorsService>().EvaluatePolicy(context, policy!);

        Assert.False(result.IsOriginAllowed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://portal.example/path")]
    [InlineData("https://user@portal.example")]
    public void FileSystemBridgeRejectsUnsafeAllowedOriginConfiguration(string origin)
    {
        var options = new DocumentUploadBridgeCorsOptions([origin]);

        Assert.Throws<InvalidOperationException>(options.GetValidatedOrigins);
    }

    private static ServiceProvider CreateProvider(string origin)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCors(options => DocumentUploadBridgeCorsPolicy.Configure(
            options, new DocumentUploadBridgeCorsOptions([origin])));
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Preflight(string origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Options;
        context.Request.Headers.Origin = origin;
        context.Request.Headers.AccessControlRequestMethod = HttpMethods.Put;
        context.Request.Headers.AccessControlRequestHeaders = "content-type";
        return context;
    }
}
