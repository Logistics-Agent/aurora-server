using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Enums;
using IamTenant.Domain.Enums;

namespace IamTenant.Application.Commands.Auth;

public record LoginResult(string AccessToken, string RefreshToken, string RefreshTokenSubject, int ExpiresIn, string UserId, string TenantId, string Role, List<string> Permissions);

public record LoginCommand(string TenantCode, string Email, string Password) : IRequest<LoginResult>;

public class LoginCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context, ISender mediator) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim();
        if (string.IsNullOrWhiteSpace(tenantCode))
            throw new Shared.Exceptions.DomainException("Tenant code is required.");

        var email = request.Email.Trim();
        if (string.Equals(tenantCode, "SYSTEM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tenantCode, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tenantCode, "SYSTEMADMIN", StringComparison.OrdinalIgnoreCase))
        {
            var systemAuthResult = await cognitoService.InitiateAuthAsync(email, request.Password, cancellationToken);

            if (systemAuthResult.Session != null)
            {
                throw new Shared.Exceptions.ForbiddenException($"NEW_PASSWORD_REQUIRED:{systemAuthResult.Session}");
            }

            var matchingUsers = await context.Users
                .IgnoreQueryFilters()
                .Where(u => (u.Email == email || u.Email.ToLower() == email.ToLower()) && !u.IsDeleted)
                .ToListAsync(cancellationToken);

            var systemUser = matchingUsers.FirstOrDefault(u => u.TenantId == null || u.Role == BaseRole.SystemAdmin)
                ?? matchingUsers.FirstOrDefault();

            if (systemUser == null)
            {
                systemUser = new Domain.User
                {
                    Id = Guid.NewGuid(),
                    TenantId = null,
                    Email = email,
                    Role = BaseRole.SystemAdmin,
                    Status = UserStatus.Active,
                    PermissionVersion = 1,
                    FirstName = "System",
                    LastName = "Admin"
                };
                context.Users.Add(systemUser);
                await context.SaveChangesAsync(cancellationToken);
            }

            var systemPermissions = await mediator.Send(new IamTenant.Application.Queries.Permissions.GetUserPermissionsQuery(systemUser.Id, systemUser.PermissionVersion), cancellationToken);

            return new LoginResult(
                systemAuthResult.AccessToken,
                systemAuthResult.RefreshToken,
                systemAuthResult.RefreshTokenSubject,
                systemAuthResult.ExpiresIn,
                systemUser.Id.ToString(),
                systemUser.TenantId is { } systemTenantId && systemTenantId != Guid.Empty
                    ? systemTenantId.ToString()
                    : string.Empty,
                systemUser.Role.ToCode(),
                systemPermissions.Permissions.Select(p => p.Code).ToList());
        }

        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => (t.Code == tenantCode || t.Code.ToUpper() == tenantCode.ToUpper()) && !t.IsDeleted, cancellationToken)
            ?? throw new Shared.Exceptions.NotFoundException("Tenant not found.");

        if (tenant.Status != TenantStatus.Active)
            throw new Shared.Exceptions.ForbiddenException("Tenant is suspended.");

        // 1. Fetch User base info from DB within the requested tenant
        var user = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => (u.Email == email || u.Email.ToLower() == email.ToLower()) && !u.IsDeleted && u.TenantId == tenant.Id)
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
            throw new Shared.Exceptions.ForbiddenException($"NEW_PASSWORD_REQUIRED:{authResult.Session}");
        }

        // 3. Fetch Permissions from Cache / DB
        var userPermissions = await mediator.Send(new IamTenant.Application.Queries.Permissions.GetUserPermissionsQuery(user.Id, user.PermissionVersion), cancellationToken);

        return new LoginResult(
            authResult.AccessToken,
            authResult.RefreshToken,
            authResult.RefreshTokenSubject,
            authResult.ExpiresIn,
            user.Id.ToString(),
            user.TenantId is { } tenantId && tenantId != Guid.Empty ? tenantId.ToString() : string.Empty,
            user.Role.ToCode(),
            userPermissions.Permissions.Select(p => p.Code).ToList());
    }
}
