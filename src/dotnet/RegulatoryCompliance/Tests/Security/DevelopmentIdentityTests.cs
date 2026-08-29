using Grpc.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RegulatoryCompliance.Tests.Grpc;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests.Security;

public sealed class DevelopmentIdentityTests
{
    private static readonly Guid DevelopmentUserId =
        Guid.Parse("01910000-0000-7000-8000-000000000001");
    private static readonly Guid DevelopmentTenantId =
        Guid.Parse("01920000-0000-7000-8000-000000000001");

    [Fact]
    public async Task MissingMetadataUsesConfiguredIdentityInDevelopment()
    {
        var currentUser = new CurrentUserService();
        var interceptor = Interceptor(currentUser, Environments.Development);

        await InvokeAsync(interceptor);

        Assert.Equal(DevelopmentUserId, currentUser.UserId);
        Assert.Equal(DevelopmentTenantId, currentUser.TenantId);
        Assert.Equal(1, currentUser.PermissionVersion);
        Assert.Equal("local-admin", currentUser.Role);
        Assert.Equal(
            [
                "regulatory-compliance.sources.ingest",
                "regulatory-compliance.sources.ingest-platform"
            ],
            currentUser.Permissions);
    }

    [Fact]
    public async Task TrustedMetadataOverridesConfiguredDevelopmentIdentity()
    {
        var userId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        var interceptor = Interceptor(currentUser, Environments.Development);
        var headers = new Metadata
        {
            { GrpcMetadataKeys.UserId, userId.ToString() },
            { GrpcMetadataKeys.TenantId, tenantId.ToString() },
            { GrpcMetadataKeys.PermissionVersion, "7" },
            { GrpcMetadataKeys.Role, "MANAGER" }
        };

        await InvokeAsync(interceptor, headers);

        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal(tenantId, currentUser.TenantId);
        Assert.Equal(7, currentUser.PermissionVersion);
        Assert.Equal("MANAGER", currentUser.Role);
        Assert.Empty(currentUser.Permissions);
    }

    [Fact]
    public async Task EnabledIdentityIsIgnoredOutsideDevelopment()
    {
        var currentUser = new CurrentUserService();
        var interceptor = Interceptor(currentUser, Environments.Production);

        await InvokeAsync(interceptor);

        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.TenantId);
        Assert.Empty(currentUser.Permissions);
    }

    [Fact]
    public async Task DisabledIdentityIsIgnoredInDevelopment()
    {
        var currentUser = new CurrentUserService();
        var interceptor = Interceptor(
            currentUser,
            Environments.Development,
            enabled: false);

        await InvokeAsync(interceptor);

        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.TenantId);
        Assert.Empty(currentUser.Permissions);
    }

    [Fact]
    public void EnabledIdentityRequiresUserAndTenantIdentifiers()
    {
        var options = new DevelopmentIdentityOptions { Enabled = true };

        Assert.False(DevelopmentIdentityOptions.IsValid(options));
    }

    [Fact]
    public async Task PartialMetadataDoesNotFallBackToDevelopmentIdentity()
    {
        var currentUser = new CurrentUserService();
        var interceptor = Interceptor(currentUser, Environments.Development);
        var headers = new Metadata
        {
            { GrpcMetadataKeys.UserId, Guid.CreateVersion7().ToString() }
        };

        await InvokeAsync(interceptor, headers);

        Assert.NotNull(currentUser.UserId);
        Assert.Null(currentUser.TenantId);
        Assert.Empty(currentUser.Permissions);
    }

    private static AuthInterceptor Interceptor(
        ICurrentUserContext currentUser,
        string environmentName,
        bool enabled = true)
    {
        var options = Options.Create(new DevelopmentIdentityOptions
        {
            Enabled = enabled,
            UserId = DevelopmentUserId,
            TenantId = DevelopmentTenantId,
            PermissionVersion = 1,
            Role = "local-admin",
            Permissions =
            [
                "regulatory-compliance.sources.ingest",
                "regulatory-compliance.sources.ingest-platform"
            ]
        });

        return new AuthInterceptor(
            currentUser,
            new TestHostEnvironment(environmentName),
            options,
            NullLogger<AuthInterceptor>.Instance);
    }

    private static Task<object> InvokeAsync(
        AuthInterceptor interceptor,
        Metadata? headers = null) =>
        interceptor.UnaryServerHandler(
            new object(),
            TestServerCallContext.Create(headers),
            static (_, _) => Task.FromResult(new object()));

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "DevelopmentIdentityTests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
