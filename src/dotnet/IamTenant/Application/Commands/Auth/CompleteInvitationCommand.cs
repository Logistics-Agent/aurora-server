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

public class CompleteInvitationCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context) : IRequestHandler<CompleteInvitationCommand, LoginResult>
{
    public async Task<LoginResult> Handle(CompleteInvitationCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("User not found in database.");

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

        var authResult = await cognitoService.CompleteNewPasswordChallengeAsync(
            clientId,
            request.Email,
            request.NewPassword,
            request.ConfirmationCode,
            cancellationToken);

        var permissions = user.UserPermissions
            .Where(up => up.Permission != null)
            .Select(up => up.Permission!.Code)
            .Distinct()
            .ToList();

        // 3. Mark User as ACTIVE if they were PENDING/INVITED
        if (user.Status != UserStatus.Active)
        {
            user.Status = UserStatus.Active;
            await context.SaveChangesAsync(cancellationToken);
        }

        return new LoginResult(
            authResult.AccessToken,
            authResult.RefreshToken,
            authResult.ExpiresIn,
            user.Id.ToString(),
            user.TenantId == Guid.Empty ? "" : user.TenantId.ToString(),
            user.Role.ToCode(),
            permissions);
    }
}
