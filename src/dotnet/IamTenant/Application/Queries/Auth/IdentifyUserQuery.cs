using IamTenant.Domain.Enums;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace IamTenant.Application.Queries.Auth;

public record IdentifyUserResult(
    bool Exists, 
    string? TenantCode, 
    string? UserType,
    Guid? UserId = null,
    Guid? TenantId = null,
    int? PermissionVersion = null,
    string? Role = null);

public record IdentifyUserQuery(string Email) : IRequest<IdentifyUserResult>;

public class IdentifyUserQueryHandler(IamTenantDbContext context) : IRequestHandler<IdentifyUserQuery, IdentifyUserResult>
{
    public async Task<IdentifyUserResult> Handle(IdentifyUserQuery request, CancellationToken cancellationToken)
    {
        // Global query filter takes care of IsDeleted, but here we query by email across tenants, 
        // so we must use IgnoreQueryFilters() if the current context tenantId is set to something else.
        var email = request.Email.Trim();
        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Where(u => (u.Email == email || u.Email.ToLower() == email.ToLower() || (u.CognitoSub != null && u.CognitoSub == email)) && !u.IsDeleted)
            .Select(u => new 
            { 
                u.Id,
                u.TenantId,
                u.Role, 
                u.PermissionVersion,
                TenantCode = u.Tenant != null ? u.Tenant.Code : null 
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return new IdentifyUserResult(false, null, null);
        }

        var isSystemAdmin = user.TenantId == null || user.Role == BaseRole.SystemAdmin;
        var tenantCode = user.TenantCode ?? (isSystemAdmin ? "SYSTEM" : "STAFF");

        return new IdentifyUserResult(
            true, 
            tenantCode, 
            user.Role.ToCode(),
            user.Id,
            user.TenantId,
            user.PermissionVersion,
            user.Role.ToCode());
    }
}
