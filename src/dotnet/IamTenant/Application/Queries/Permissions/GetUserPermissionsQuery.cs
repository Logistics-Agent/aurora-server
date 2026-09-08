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
            .IgnoreQueryFilters()
            .Where(u => u.Id == request.UserId && !u.IsDeleted)
            .Select(u => new
            {
                u.PermissionVersion,
                u.TenantId,
                RoleCode = u.Role.ToCode()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User not found");

        var permissions = await context.UserPermissions
            .IgnoreQueryFilters()
            .Where(up => up.UserId == request.UserId && up.Permission != null)
            .Select(up => new PermissionDto
            {
                Id = up.Permission!.Id,
                Code = up.Permission.Code,
                Module = up.Permission.Module,
                Description = up.Permission.Description
            })
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        // Auto-heal: If direct permissions are empty in DB, seed from authoritative role template
        if (permissions.Count == 0 && !string.IsNullOrWhiteSpace(user.RoleCode))
        {
            var defaultCodes = user.RoleCode.Trim().ToUpperInvariant() switch
            {
                "SYSTEMADMIN" or "SYSTEM_ADMIN" => Shared.Constants.PermissionConstants.GetAllPermissions(),
                "TENANTADMIN" or "TENANT_ADMIN" => Shared.Constants.PermissionConstants.GetTenantAdminPermissions(),
                "MANAGER" => Shared.Constants.PermissionConstants.GetDefaultManagerPermissions(),
                "STAFF" => Shared.Constants.PermissionConstants.GetDefaultStaffPermissions(),
                _ => (IReadOnlyList<string>)Array.Empty<string>()
            };

            if (defaultCodes.Count > 0)
            {
                var existingPerms = await context.Permissions
                    .Where(p => defaultCodes.Contains(p.Code))
                    .ToListAsync(cancellationToken);

                var existingCodeMap = existingPerms.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

                foreach (var code in defaultCodes)
                {
                    if (!existingCodeMap.TryGetValue(code, out var perm))
                    {
                        var parts = code.Split(':');
                        perm = new Domain.Permission
                        {
                            Id = IamTenantDbContext.DeterministicPermissionId(code),
                            Code = code,
                            Module = parts[0],
                            Description = $"Allows {code}"
                        };
                        context.Permissions.Add(perm);
                        existingCodeMap[code] = perm;
                    }

                    context.UserPermissions.Add(new Domain.UserPermission
                    {
                        UserId = request.UserId,
                        PermissionId = perm.Id,
                        TenantId = user.TenantId,
                        GrantedAt = DateTimeOffset.UtcNow
                    });
                }

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    // Ignore concurrency/conflict on concurrent auto-healing
                }

                permissions = existingCodeMap.Values.Select(p => new PermissionDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Module = p.Module,
                    Description = p.Description
                }).OrderBy(p => p.Code).ToList();
            }
        }

        var newCache = new UserPermissionCache
        {
            Version = user.PermissionVersion,
            Role = user.RoleCode,
            Permissions = [.. permissions.Select(p => p.Code)]
        };
        await permissionCache.SetAsync(request.UserId, newCache, cancellationToken);

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            Role = user.RoleCode,
            Permissions = permissions,
            Version = user.PermissionVersion,
            FromCache = false
        };
    }
}
