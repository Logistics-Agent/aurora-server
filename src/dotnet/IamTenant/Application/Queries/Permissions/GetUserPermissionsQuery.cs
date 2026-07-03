using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;

namespace IamTenant.Application.Queries.Permissions;

// ─────────────────────────────────────────────────────────────────────────────
// GET USER PERMISSIONS — Redis-first, DB fallback, version check
// ─────────────────────────────────────────────────────────────────────────────
public record GetUserPermissionsQuery(Guid UserId, int? JwtPermissionVersion = null) : IRequest<UserPermissionsDto>;

public class GetUserPermissionsHandler(
    IamTenantDbContext context,
    IPermissionCacheService permissionCache)
    : IRequestHandler<GetUserPermissionsQuery, UserPermissionsDto>
{
    public async Task<UserPermissionsDto> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var cached = await permissionCache.GetAsync(request.UserId, cancellationToken);

        if (cached is not null && (request.JwtPermissionVersion is null || cached.Version == request.JwtPermissionVersion))
        {
            return new UserPermissionsDto
            {
                UserId = request.UserId,
                RoleIds = cached.RoleIds,
                RoleCodes = cached.RoleCodes,
                Permissions = [.. cached.Permissions.Select(p => new PermissionDto { Code = p })],
                Version = cached.Version,
                FromCache = true
            };
        }

        var userData = await context.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new
            {
                u.PermissionVersion,
                RoleIds = u.UserRoles.SelectMany(ur => ur.RoleIds).ToList(),
                RoleCodes = u.UserRoles.Select(ur => ur.Role!.Code).ToList(),
                RolePermissions = u.UserRoles
                    .SelectMany(ur => ur.Role!.RolePermissions.Select(rp => rp.Permission))
                    .Where(p => p != null)
                    .Select(p => new { p!.Id, p.Code, p.Module, p.Description })
                    .ToList(),

                DirectPermissions = u.UserPermissions
                    .Select(up => up.Permission)
                    .Where(p => p != null)
                    .Select(p => new { p!.Id, p.Code, p.Module, p.Description })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new Shared.Exceptions.NotFoundException("User not found");
        
        var permissions = userData.RolePermissions
            .Concat(userData.DirectPermissions)
            .DistinctBy(p => p.Id)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Module = p.Module,
                Description = p.Description
            })
            .ToList();

        var newCache = new UserPermissionCache
        {
            Version = userData.PermissionVersion,
            RoleIds = userData.RoleIds,
            RoleCodes = userData.RoleCodes,
            Permissions = [.. permissions.Select(p => p.Code)]
        };
        await permissionCache.SetAsync(request.UserId, newCache, cancellationToken);

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            RoleIds = userData.RoleIds,
            RoleCodes = userData.RoleCodes,
            Permissions = permissions,
            Version = userData.PermissionVersion,
            FromCache = false
        };
    }
}
