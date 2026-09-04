using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Enums;
using IamTenant.Domain.Enums;

namespace IamTenant.Application.Commands.Auth;

public record LoginResult(string AccessToken, string RefreshToken, int ExpiresIn, string UserId, string TenantId, string Role, List<string> Permissions);

public record LoginCommand(string TenantCode, string Email, string Password) : IRequest<LoginResult>;

public class LoginCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context, ISender mediator) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim();
        if (string.IsNullOrWhiteSpace(tenantCode))
            throw new Shared.Exceptions.DomainException("Tenant code is required.");

        if (string.Equals(tenantCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            var systemUser = await context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Email == request.Email && !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.TenantId,
                    u.Status,
                    u.PermissionVersion,
                    u.Role
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new Shared.Exceptions.NotFoundException("User not found");

            var systemAuthResult = await cognitoService.InitiateAuthAsync(request.Email, request.Password, cancellationToken);

            if (systemAuthResult.Session != null)
            {
                throw new Shared.Exceptions.ForbiddenException("NEW_PASSWORD_REQUIRED. Please complete invitation.");
            }

            var systemPermissions = await mediator.Send(new IamTenant.Application.Queries.Permissions.GetUserPermissionsQuery(systemUser.Id, systemUser.PermissionVersion), cancellationToken);

            return new LoginResult(
                systemAuthResult.AccessToken,
                systemAuthResult.RefreshToken,
                systemAuthResult.ExpiresIn,
                systemUser.Id.ToString(),
                systemUser.TenantId == Guid.Empty ? string.Empty : systemUser.TenantId.ToString(),
                systemUser.Role.ToCode(),
                systemPermissions.Permissions.Select(p => p.Code).ToList());
        }

        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == tenantCode && !t.IsDeleted, cancellationToken)
            ?? throw new Shared.Exceptions.NotFoundException("Tenant not found.");

        if (tenant.Status != TenantStatus.Active)
            throw new Shared.Exceptions.ForbiddenException("Tenant is suspended.");

        // 1. Fetch User base info from DB within the requested tenant
        var user = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Email == request.Email && !u.IsDeleted && u.TenantId == tenant.Id)
            .Select(u => new
            {
                u.Id,
                u.TenantId,
                u.Status,
                u.PermissionVersion,
                u.Role
            })
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new Shared.Exceptions.NotFoundException("User not found");

        var clientId = user.Role == Shared.Enums.BaseRole.TenantAdmin
            ? tenant.AdminUserPoolClientId
            : tenant.StaffUserPoolClientId;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = tenant.AdminUserPoolClientId ?? tenant.StaffUserPoolClientId;
        }

        if (string.IsNullOrWhiteSpace(clientId))
            throw new Shared.Exceptions.NotFoundException("Tenant auth client is not configured.");

        // 2. Authenticate with tenant-specific Cognito client
        var authResult = await cognitoService.InitiateAuthAsync(clientId, request.Email, request.Password, cancellationToken);

        if (authResult.Session != null)
        {
            throw new Shared.Exceptions.ForbiddenException("NEW_PASSWORD_REQUIRED. Please complete invitation.");
        }

        // 3. Fetch Permissions from Cache / DB
        var userPermissions = await mediator.Send(new IamTenant.Application.Queries.Permissions.GetUserPermissionsQuery(user.Id, user.PermissionVersion), cancellationToken);

        return new LoginResult(
            authResult.AccessToken,
            authResult.RefreshToken,
            authResult.ExpiresIn,
            user.Id.ToString(),
            user.TenantId == Guid.Empty ? "" : user.TenantId.ToString(),
            user.Role.ToCode(),
            userPermissions.Permissions.Select(p => p.Code).ToList());
    }
}
