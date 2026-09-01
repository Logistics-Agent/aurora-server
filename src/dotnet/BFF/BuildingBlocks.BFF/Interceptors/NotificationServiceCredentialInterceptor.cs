using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;
using Shared.Security;

namespace BuildingBlocks.BFF.Interceptors;

public sealed class NotificationServiceCredentialInterceptor(
    IOptions<NotificationServiceCredentialOptions> options) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var apiKey = options.Value.ServiceApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Grpc:Notification:ServiceApiKey is required.");

        var headers = context.Options.Headers ?? new Metadata();
        headers.Add(GrpcMetadataKeys.ServiceId, "staff-bff");
        headers.Add(GrpcMetadataKeys.ServiceApiKey, apiKey);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
        return continuation(request, newContext);
    }
}
