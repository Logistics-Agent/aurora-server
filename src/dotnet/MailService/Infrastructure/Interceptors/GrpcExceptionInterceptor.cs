using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace MailService.Infrastructure.Interceptors;

public class GrpcExceptionInterceptor : Interceptor
{
    private readonly ILogger<GrpcExceptionInterceptor> _logger;

    public GrpcExceptionInterceptor(ILogger<GrpcExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            // Already an RPC exception with appropriate StatusCode; pass through directly
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "gRPC InvalidArgument on {Method}: {Message}", context.Method, ex.Message);
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "gRPC NotFound on {Method}: {Message}", context.Method, ex.Message);
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "gRPC PermissionDenied on {Method}: {Message}", context.Method, ex.Message);
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "gRPC FailedPrecondition on {Method}: {Message}", context.Method, ex.Message);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "gRPC DeadlineExceeded on {Method}: {Message}", context.Method, ex.Message);
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "The requested operation timed out."));
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("gRPC call cancelled by client on {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Cancelled, "Client cancelled the request."));
        }
        catch (Exception ex)
        {
            // Sanitize internal server error to prevent leaking connection strings, secrets, or internal SQL
            _logger.LogError(ex, "Unhandled internal error on gRPC {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "An internal error occurred processing your request."));
        }
    }
}
