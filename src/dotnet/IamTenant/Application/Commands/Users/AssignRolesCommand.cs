using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;

namespace IamTenant.Application.Commands.Users;

/// <summary>
/// ASSIGN ROLES TO USER
/// Sau khi assign, invalidate Redis cache của user đó.
/// </summary>
public record AssignRolesCommand(Guid UserId, List<Guid> RoleIds) : IRequest<StaffDto>;

public class AssignRolesHandler(
    IamTenantDbContext context,
    IPermissionCacheService permissionCache)
    : IRequestHandler<AssignRolesCommand, StaffDto>
{
    public async Task<StaffDto> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new Exception("User not found.");

        var roleIds = request.RoleIds
            .Distinct()
            .OrderBy(roleId => roleId)
            .ToList();

        if (roleIds.Count > 0)
        {
            var existingRoles = await context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Code })
                .ToListAsync(cancellationToken);

            if (existingRoles.Count != roleIds.Count)
            {
                var existingRoleIds = existingRoles.Select(r => r.Id).ToList();
                var missingRoleIds = roleIds.Except(existingRoleIds).ToList();
                throw new Exception($"Role(s) not found: {string.Join(", ", missingRoleIds)}.");
            }

            var hasAdminRole = existingRoles.Any(r => r.Code == "TENANT_ADMIN");
            user.UserType = hasAdminRole ? Domain.Enums.UserType.TenantAdmin : Domain.Enums.UserType.TenantStaff;
        }

        var existingUserRoles = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(cancellationToken);

        context.UserRoles.RemoveRange(existingUserRoles);

        if (roleIds.Count > 0)
        {
            context.UserRoles.AddRange(roleIds.Select(r => new Domain.UserRole
            {
                UserId = user.Id,
                RoleId = r  
            }));
        }

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Redis — next request will re-build from DB
        await permissionCache.InvalidateAsync(user.Id, cancellationToken);

        return new StaffDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            Status = user.Status,
            StaffType = user.StaffType,
            CreatedAt = user.CreatedAt
        };
    }
}
