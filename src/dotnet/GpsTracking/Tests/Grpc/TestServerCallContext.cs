using Grpc.Core;

namespace GpsTracking.Tests.Grpc;

internal sealed class TestServerCallContext : ServerCallContext
{
    private readonly Metadata _requestHeaders = [];
    private readonly Metadata _responseTrailers = [];
    private Status _status;
    private WriteOptions? _writeOptions;

    internal static TestServerCallContext Create() => new();

    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "ipv4:127.0.0.1";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => _requestHeaders;
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => _responseTrailers;
    protected override Status StatusCore { get => _status; set => _status = value; }
    protected override WriteOptions? WriteOptionsCore { get => _writeOptions; set => _writeOptions = value; }
    protected override AuthContext AuthContextCore =>
        new(string.Empty, new Dictionary<string, List<AuthProperty>>());
    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options) => throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
