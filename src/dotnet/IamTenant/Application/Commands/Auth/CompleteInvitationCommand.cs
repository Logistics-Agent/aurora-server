using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IamTenant.Domain.Enums;
using Shared.Enums;

namespace IamTenant.Application.Commands.Auth;

public record CompleteInvitationCommand(string Email, string NewPassword, string ConfirmationCode) : IRequest<LoginResult>;

public class CompleteInvitationCommandHandler(
    ICognitoAuthService cognitoService, 
    IamTenantDbContext context,
    ISender mediator) : IRequestHandler<CompleteInvitationCommand, LoginResult>
{
    public async Task<LoginResult> Handle(CompleteInvitationCommand request, CancellationToken cancellationToken)
    {
        var matchingUsers = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .Where(u => (u.Email == request.Email || u.Email.ToLower() == request.Email.ToLower()) && !u.IsDeleted)
            .ToListAsync(cancellationToken);

        var user = matchingUsers.FirstOrDefault(u => u.TenantId == null || u.Role == BaseRole.SystemAdmin)
            ?? matchingUsers.FirstOrDefault()
            ?? throw new Exception("User not found in database.");

        var isSystem = user.TenantId == null || user.Role == BaseRole.SystemAdmin;
        AuthResult authResult;

        if (isSystem)
        {
            authResult = await cognitoService.CompleteNewPasswordChallengeAsync(
                request.Email,
                request.NewPassword,
                request.ConfirmationCode,
                cancellationToken);
        }
        else
        {
            var tenant = user.Tenant ?? throw new Exception("Tenant not found for user.");
            var clientId = user.Role == BaseRole.TenantAdmin
                ? tenant.AdminUserPoolClientId
                : tenant.StaffUserPoolClientId;

            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = tenant.AdminUserPoolClientId ?? tenant.StaffUserPoolClientId;
            }

            if (string.IsNullOrWhiteSpace(clientId))
                throw new Exception("Tenant auth client is not configured.");

            authResult = await cognitoService.CompleteNewPasswordChallengeAsync(
                clientId,
                request.Email,
                request.NewPassword,
                request.ConfirmationCode,
                cancellationToken);
        }

        // 3. Mark User as ACTIVE if they were PENDING/INVITED
        if (user.Status != UserStatus.Active)
        {
            user.Status = UserStatus.Active;
            await context.SaveChangesAsync(cancellationToken);
        }

        // 4. Load & Warm up permissions in Redis cache
        var userPermissions = await mediator.Send(
            new IamTenant.Application.Queries.Permissions.GetUserPermissionsQuery(user.Id, user.PermissionVersion),
            cancellationToken);

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
