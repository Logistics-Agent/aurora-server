using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Application.DTOs.Roles;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;
using Shared.Constants;
using Shared.Enums;
using Shared.Exceptions;
using Shared.Security;

namespace IamTenant.Application.Commands.Permissions;

public record UpdateUserPermissionsCommand(
    Guid UserId,
    List<string>? Grant = null,
    List<string>? Revoke = null) : IRequest<UserPermissionsDto>;

public class UpdateUserPermissionsHandler(
    IamTenantDbContext context,
    ICurrentUserService currentUser,
    IPermissionCacheService permissionCache)
    : IRequestHandler<UpdateUserPermissionsCommand, UserPermissionsDto>
{
    public async Task<UserPermissionsDto> Handle(UpdateUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new ForbiddenException("Tenant context is required.");

        var user = await context.Users
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == currentUser.TenantId.Value && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' not found in tenant.");

        var grantCodes = (request.Grant ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var revokeCodes = (request.Revoke ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Security Invariant: Cannot grant system-only permissions from tenant context
        var systemPermsAttempted = grantCodes
            .Where(PermissionConstants.IsSystemOnlyPermission)
            .ToList();

        if (systemPermsAttempted.Count > 0)
            throw new DomainException($"Cannot grant platform system-only permissions: {string.Join(", ", systemPermsAttempted)}");

        // Validate that grant codes exist in DB catalog
        if (grantCodes.Count > 0)
        {
            var existingPerms = await context.Permissions
                .Where(p => grantCodes.Contains(p.Code))
                .ToListAsync(cancellationToken);

            var existingPermMap = existingPerms.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);
            var unknownCodes = grantCodes.Where(c => !existingPermMap.ContainsKey(c)).ToList();
            if (unknownCodes.Count > 0)
                throw new DomainException($"Unknown permission codes: {string.Join(", ", unknownCodes)}");

            var currentPermCodes = user.UserPermissions
                .Where(up => up.Permission != null)
                .Select(up => up.Permission!.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var code in grantCodes)
            {
                if (!currentPermCodes.Contains(code))
                {
                    var perm = existingPermMap[code];
                    user.UserPermissions.Add(new UserPermission
                    {
                        UserId = user.Id,
                        PermissionId = perm.Id,
                        TenantId = user.TenantId,
                        GrantedByUserId = currentUser.UserId,
                        GrantedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        // Idempotent Revoke
        if (revokeCodes.Count > 0)
        {
            var toRemove = user.UserPermissions
                .Where(up => up.Permission != null && revokeCodes.Contains(up.Permission.Code))
                .ToList();

            foreach (var item in toRemove)
            {
                context.UserPermissions.Remove(item);
            }
        }

        user.PermissionVersion++;

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Redis cache
        await permissionCache.InvalidateAsync(user.Id, cancellationToken);

        // Reload permissions
        var activePermissions = await context.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => new PermissionDto
            {
                Id = up.Permission!.Id,
                Code = up.Permission.Code,
                Module = up.Permission.Module,
                Description = up.Permission.Description
            })
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return new UserPermissionsDto
        {
            UserId = user.Id,
            Role = user.Role.ToCode(),
            Permissions = activePermissions,
            Version = user.PermissionVersion,
            FromCache = false
        };
    }
}
