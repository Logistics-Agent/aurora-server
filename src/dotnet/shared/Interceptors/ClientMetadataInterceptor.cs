using Grpc.Core;
using Grpc.Core.Interceptors;
using Shared.Security;

namespace Shared.Interceptors;

/// <summary>
/// gRPC Client-side interceptor: forward x-user-id, x-tenant-id, x-trace-id
/// khi BFF gọi sang các gRPC microservices khác.
/// </summary>
public class ClientMetadataInterceptor(ICurrentUserService currentUser, string? serviceId = null) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        AppendMetadata(headers);

        var newOptions = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, newOptions);

        return continuation(request, newContext);
    }

    private void AppendMetadata(Metadata headers)
    {
        if (!string.IsNullOrEmpty(serviceId))
            headers.Add(GrpcMetadataKeys.ServiceId, serviceId);
        if (currentUser.UserId.HasValue)
            headers.Add(GrpcMetadataKeys.UserId, currentUser.UserId.ToString()!);
        if (currentUser.TenantId.HasValue)
            headers.Add(GrpcMetadataKeys.TenantId, currentUser.TenantId.ToString()!);
        if (!string.IsNullOrEmpty(currentUser.TraceId))
            headers.Add(GrpcMetadataKeys.TraceId, currentUser.TraceId);
        if (currentUser.PermissionVersion.HasValue)
            headers.Add(GrpcMetadataKeys.PermissionVersion, currentUser.PermissionVersion.ToString()!);
        if (!string.IsNullOrEmpty(currentUser.Role))
            headers.Add(GrpcMetadataKeys.Role, currentUser.Role);
    }
}

