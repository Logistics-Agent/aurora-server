using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IamTenant.Domain.Enums;

namespace IamTenant.Application.Commands.Auth;

public record CompleteInvitationCommand(string Email, string NewPassword, string ConfirmationCode) : IRequest<LoginResult>;

public class CompleteInvitationCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context) : IRequestHandler<CompleteInvitationCommand, LoginResult>
{
    public async Task<LoginResult> Handle(CompleteInvitationCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("User not found in database.");

        var tenant = user.Tenant ?? throw new Exception("Tenant not found for user.");

        var clientId = user.UserType == UserType.TenantAdmin
            ? tenant.AdminUserPoolClientId
            : tenant.UserUserPoolClientId;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = tenant.AdminUserPoolClientId ?? tenant.UserUserPoolClientId;
        }

        if (string.IsNullOrWhiteSpace(clientId))
            throw new Exception("Tenant auth client is not configured.");

        var authResult = await cognitoService.CompleteNewPasswordChallengeAsync(
            clientId,
            request.Email,
            request.NewPassword,
            request.ConfirmationCode,
            cancellationToken);

        // Fetch Roles and Permissions via Projection to avoid deep Includes
        var userPermissions = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => new
            {
                RoleCode = ur.Role!.Code,
                Permissions = ur.Role.RolePermissions.Select(rp => rp.Permission!.Code).ToList()
            })
            .ToListAsync(cancellationToken);

        var roles = userPermissions.Select(x => x.RoleCode).Distinct().ToList();
        var permissions = userPermissions.SelectMany(x => x.Permissions).Distinct().ToList();

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
            roles,
            permissions);
    }
}
