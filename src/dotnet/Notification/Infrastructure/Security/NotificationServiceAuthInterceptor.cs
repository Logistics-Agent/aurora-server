using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;
using Shared.Security;

namespace Notification.Infrastructure.Security;

public sealed class NotificationServiceAuthInterceptor(
    IOptions<NotificationServiceAuthOptions> options,
    ILogger<NotificationServiceAuthInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        EnsureServiceCredential(context);
        return await continuation(request, context);
    }

    private void EnsureServiceCredential(ServerCallContext context)
    {
        var configured = options.Value;
        var serviceId = context.RequestHeaders.GetValue(GrpcMetadataKeys.ServiceId);
        var apiKey = context.RequestHeaders.GetValue(GrpcMetadataKeys.ServiceApiKey);

        if (!EqualsSecret(serviceId, configured.AllowedServiceId) || !EqualsSecret(apiKey, configured.ApiKey))
        {
            logger.LogWarning("Notification gRPC service authentication failed: invalid service credential.");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid service credential."));
        }
    }

    private static bool EqualsSecret(string? supplied, string configured)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(configured)) return false;
        var suppliedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var configuredDigest = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(suppliedDigest, configuredDigest);
    }
}
