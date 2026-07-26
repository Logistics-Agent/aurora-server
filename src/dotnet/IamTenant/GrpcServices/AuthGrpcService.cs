using Grpc.Core;
using IamTenant.Application.Commands.Auth;
using IamTenant.Application.Queries.Auth;
using IamTenant.Application.Interfaces;
using MediatR;
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
                UserType = result.UserType ?? ""
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
            var result = await mediator.Send(new LoginCommand(request.Email, request.Password), context.CancellationToken);

            var response = new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                UserId = result.UserId,
                TenantId = result.TenantId
            };

            response.Roles.AddRange(result.Roles);
            response.Permissions.AddRange(result.Permissions);

            return response;
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

            response.Roles.AddRange(result.Roles);
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
            var authResult = await cognitoService.RefreshTokenAsync(string.Empty, request.RefreshToken, context.CancellationToken);
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
            await cognitoService.ForgotPasswordAsync(request.Email, context.CancellationToken);
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
            await cognitoService.ConfirmForgotPasswordAsync(request.Email, request.NewPassword, request.ConfirmationCode, context.CancellationToken);
            return new EmptyResponse();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }
}
