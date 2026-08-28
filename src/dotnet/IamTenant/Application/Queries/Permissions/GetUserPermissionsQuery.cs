using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;
using Shared.Enums;
using Shared.Exceptions;

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
                Role = cached.Role,
                Permissions = [.. cached.Permissions.Select(p => new PermissionDto { Code = p })],
                Version = cached.Version,
                FromCache = true
            };
        }

        var user = await context.Users
            .Where(u => u.Id == request.UserId && !u.IsDeleted)
            .Select(u => new
            {
                u.PermissionVersion,
                RoleCode = u.Role.ToCode(),
                Permissions = u.UserPermissions
                    .Where(up => up.Permission != null)
                    .Select(up => new PermissionDto
                    {
                        Id = up.Permission!.Id,
                        Code = up.Permission.Code,
                        Module = up.Permission.Module,
                        Description = up.Permission.Description
                    })
                    .OrderBy(p => p.Code)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User not found");

        var newCache = new UserPermissionCache
        {
            Version = user.PermissionVersion,
            Role = user.RoleCode,
            Permissions = [.. user.Permissions.Select(p => p.Code)]
        };
        await permissionCache.SetAsync(request.UserId, newCache, cancellationToken);

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            Role = user.RoleCode,
            Permissions = user.Permissions,
            Version = user.PermissionVersion,
            FromCache = false
        };
    }
}
