using Grpc.Core;
using IamTenant.Application.Commands.Auth;
using IamTenant.Application.Queries.Auth;
using IamTenant.Application.Interfaces;
using MediatR;
using Shared.Enums;
using Auth.Grpc;

namespace IamTenant.GrpcServices;

public class AuthGrpcService(
    IMediator mediator,
    ICognitoAuthService cognitoService) : AuthService.AuthServiceBase
{
    public override async Task<IdentifyUserResponse> IdentifyUser(IdentifyUserRequest request, ServerCallContext context)
    {
        try
        {
            var result = await mediator.Send(new IdentifyUserQuery(request.Email), context.CancellationToken);

            return new IdentifyUserResponse
            {
                Exists = result.Exists,
                TenantCode = result.TenantCode ?? "",
                UserType = result.UserType ?? "",
                UserId = result.UserId?.ToString() ?? "",
                TenantId = result.TenantId?.ToString() ?? "",
                PermissionVersion = result.PermissionVersion ?? 0,
                Role = result.Role ?? ""
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        try
        {
            var result = await mediator.Send(new LoginCommand(request.TenantCode, request.Email, request.Password), context.CancellationToken);

            var response = new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                UserId = result.UserId,
                TenantId = result.TenantId
            };

            response.Roles.Add(result.Role);
            response.Permissions.AddRange(result.Permissions);

            return response;
        }
        catch (Shared.Exceptions.ForbiddenException ex) when (ex.Message.StartsWith("NEW_PASSWORD_REQUIRED"))
        {
            var session = ex.Message.Contains(':') ? ex.Message.Split(':', 2)[1] : "";
            throw new RpcException(new Status(StatusCode.FailedPrecondition, session));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
    }

    public override async Task<LoginResponse> CompleteInvitation(CompleteInvitationRequest request, ServerCallContext context)
    {
        try
        {
            var result = await mediator.Send(new CompleteInvitationCommand(request.Email, request.NewPassword, request.ConfirmationCode), context.CancellationToken);

            var response = new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                UserId = result.UserId,
                TenantId = result.TenantId
            };

            response.Roles.Add(result.Role);
            response.Permissions.AddRange(result.Permissions);

            return response;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<LoginResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
    {
        try
        {
            var authResult = IsSystemUser(request.TenantCode, request.UserType)
                ? await cognitoService.RefreshTokenAsync(string.Empty, request.RefreshToken, context.CancellationToken)
                : await cognitoService.RefreshTokenAsync(
                    await mediator.Send(
                        new ResolveTenantAuthClientQuery(request.TenantCode, request.UserType),
                        context.CancellationToken),
                    request.RefreshToken,
                    context.CancellationToken);
            return new LoginResponse
            {
                AccessToken = authResult.AccessToken,
                RefreshToken = authResult.RefreshToken,
                ExpiresIn = authResult.ExpiresIn
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
    }

    public override Task<EmptyResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        return Task.FromResult(new EmptyResponse());
    }

    public override async Task<EmptyResponse> ForgotPassword(ForgotPasswordRequest request, ServerCallContext context)
    {
        try
        {
            var identity = await mediator.Send(new IdentifyUserQuery(request.Email), context.CancellationToken);
            if (!identity.Exists)
                throw new RpcException(new Status(StatusCode.NotFound, "User not found."));

            if (IsSystemUser(identity.TenantCode, identity.UserType))
            {
                await cognitoService.ForgotPasswordAsync(request.Email, context.CancellationToken);
            }
            else
            {
                var clientId = await mediator.Send(
                    new ResolveTenantAuthClientQuery(identity.TenantCode ?? string.Empty, identity.UserType ?? string.Empty),
                    context.CancellationToken);

                await cognitoService.ForgotPasswordAsync(clientId, request.Email, context.CancellationToken);
            }
            return new EmptyResponse();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<EmptyResponse> ConfirmForgotPassword(ConfirmForgotPasswordRequest request, ServerCallContext context)
    {
        try
        {
            var identity = await mediator.Send(new IdentifyUserQuery(request.Email), context.CancellationToken);
            if (!identity.Exists)
                throw new RpcException(new Status(StatusCode.NotFound, "User not found."));

            if (IsSystemUser(identity.TenantCode, identity.UserType))
            {
                await cognitoService.ConfirmForgotPasswordAsync(request.Email, request.NewPassword, request.ConfirmationCode, context.CancellationToken);
            }
            else
            {
                var clientId = await mediator.Send(
                    new ResolveTenantAuthClientQuery(identity.TenantCode ?? string.Empty, identity.UserType ?? string.Empty),
                    context.CancellationToken);

                await cognitoService.ConfirmForgotPasswordAsync(clientId, request.Email, request.NewPassword, request.ConfirmationCode, context.CancellationToken);
            }
            return new EmptyResponse();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<EmptyResponse> ChangePassword(ChangePasswordRequest request, ServerCallContext context)
    {
        try
        {
            var identity = await mediator.Send(new IdentifyUserQuery(request.Email), context.CancellationToken);
            if (!identity.Exists)
                throw new RpcException(new Status(StatusCode.NotFound, "User not found."));

            var tenantCode = !string.IsNullOrWhiteSpace(request.TenantCode) ? request.TenantCode : identity.TenantCode;
            var userType = !string.IsNullOrWhiteSpace(request.UserType) ? request.UserType : identity.UserType;

            if (IsSystemUser(tenantCode, userType))
            {
                await cognitoService.ChangePasswordAsync(request.Email, request.CurrentPassword, request.NewPassword, context.CancellationToken);
            }
            else
            {
                var clientId = await mediator.Send(
                    new ResolveTenantAuthClientQuery(tenantCode ?? string.Empty, userType ?? string.Empty),
                    context.CancellationToken);

                await cognitoService.ChangePasswordAsync(clientId, request.Email, request.CurrentPassword, request.NewPassword, context.CancellationToken);
            }
            return new EmptyResponse();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static bool IsSystemUser(string? tenantCode, string? userType)
    {
        if (!string.IsNullOrWhiteSpace(userType))
        {
            if (string.Equals(userType, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(userType, "SYSTEMADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(userType, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (BaseRoleExtensions.TryParseRole(userType, out var role) && role == BaseRole.SystemAdmin)
                return true;
        }

        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            if (string.Equals(tenantCode, "SYSTEM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tenantCode, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tenantCode, "SYSTEMADMIN", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
