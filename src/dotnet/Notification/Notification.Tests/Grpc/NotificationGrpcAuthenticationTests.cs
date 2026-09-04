using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notification.Infrastructure.Security;
using Shared.Security;
using Xunit;

namespace Notification.Tests.Grpc;

public sealed class NotificationGrpcAuthenticationTests
{
    private const string ServiceId = "staff-bff";
    private const string ApiKey = "test-only-service-key";

    [Fact]
    public async Task MissingServiceApiKey_ReturnsUnauthenticated()
    {
        var continuationCalled = false;
        var exception = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync([], () => continuationCalled = true));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        Assert.False(continuationCalled);
    }

    [Fact]
    public async Task WrongServiceApiKey_ReturnsUnauthenticated()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(
            [(GrpcMetadataKeys.ServiceId, ServiceId), (GrpcMetadataKeys.ServiceApiKey, "wrong")],
            () => { }));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task WrongServiceId_ReturnsUnauthenticated()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(
            [(GrpcMetadataKeys.ServiceId, "other-service"), (GrpcMetadataKeys.ServiceApiKey, ApiKey)],
            () => { }));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task ValidServiceCredential_ReachesHandler()
    {
        var continuationCalled = false;
        await InvokeAsync(
            [(GrpcMetadataKeys.ServiceId, ServiceId), (GrpcMetadataKeys.ServiceApiKey, ApiKey)],
            () => continuationCalled = true);

        Assert.True(continuationCalled);
    }

    private static Task InvokeAsync(
        IEnumerable<(string Key, string Value)> metadata,
        Action continuationCalled)
    {
        var interceptor = new NotificationServiceAuthInterceptor(
            Options.Create(new NotificationServiceAuthOptions { AllowedServiceId = ServiceId, ApiKey = ApiKey }),
            NullLogger<NotificationServiceAuthInterceptor>.Instance);
        var context = new TestServerCallContext(metadata);
        return interceptor.UnaryServerHandler(
            new Empty(),
            context,
            (_, _) =>
            {
                continuationCalled();
                return Task.FromResult(new Empty());
            });
    }

    private sealed class TestServerCallContext(IEnumerable<(string Key, string Value)> entries) : ServerCallContext
    {
        private readonly Metadata requestHeaders = CreateHeaders(entries);
        private readonly Metadata responseTrailers = [];
        private readonly Dictionary<object, object> userState = [];

        protected override string MethodCore => "notification.test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:1";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => requestHeaders;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => responseTrailers;
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());
        protected override IDictionary<object, object> UserStateCore => userState;

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;

        private static Metadata CreateHeaders(IEnumerable<(string Key, string Value)> entries)
        {
            var headers = new Metadata();
            foreach (var entry in entries) headers.Add(entry.Key, entry.Value);
            return headers;
        }
    }
}
