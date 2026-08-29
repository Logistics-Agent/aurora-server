using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;
using Shared.Constants;
using Shared.Exceptions;
using Shared.Security;

namespace IamTenant.Application.Commands.Permissions;

public record BulkUpdateUserPermissionsResultDto
{
    public int UpdatedUsersCount { get; set; }
    public List<Guid> AffectedUserIds { get; set; } = [];
}

public record BulkUpdateUserPermissionsCommand(
    List<Guid> UserIds,
    List<string>? Grant = null,
    List<string>? Revoke = null) : IRequest<BulkUpdateUserPermissionsResultDto>;

public class BulkUpdateUserPermissionsHandler(
    IamTenantDbContext context,
    ICurrentUserService currentUser,
    IPermissionCacheService permissionCache)
    : IRequestHandler<BulkUpdateUserPermissionsCommand, BulkUpdateUserPermissionsResultDto>
{
    public async Task<BulkUpdateUserPermissionsResultDto> Handle(BulkUpdateUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new ForbiddenException("Tenant context is required.");

        var targetUserIds = (request.UserIds ?? [])
            .Distinct()
            .ToList();

        if (targetUserIds.Count == 0)
            return new BulkUpdateUserPermissionsResultDto { UpdatedUsersCount = 0, AffectedUserIds = [] };

        var tenantId = currentUser.TenantId.Value;

        // Verify that ALL users exist and belong to the caller's tenant (Fail-closed tenant isolation)
        var users = await context.Users
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .Where(u => targetUserIds.Contains(u.Id) && u.TenantId == tenantId && !u.IsDeleted)
            .ToListAsync(cancellationToken);

        if (users.Count != targetUserIds.Count)
        {
            var foundIds = users.Select(u => u.Id).ToHashSet();
            var missingOrCrossTenantIds = targetUserIds.Where(id => !foundIds.Contains(id)).ToList();
            throw new DomainException($"One or more users not found or belong to another tenant: {string.Join(", ", missingOrCrossTenantIds)}");
        }

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

        // Validate Grant codes exist in DB catalog
        var existingPermMap = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);
        if (grantCodes.Count > 0)
        {
            var existingPerms = await context.Permissions
                .Where(p => grantCodes.Contains(p.Code))
                .ToListAsync(cancellationToken);

            existingPermMap = existingPerms.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);
            var unknownCodes = grantCodes.Where(c => !existingPermMap.ContainsKey(c)).ToList();
            if (unknownCodes.Count > 0)
                throw new DomainException($"Unknown permission codes: {string.Join(", ", unknownCodes)}");
        }

        foreach (var user in users)
        {
            // Grants
            if (grantCodes.Count > 0)
            {
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

            // Revokes
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
        }

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Redis cache for all affected users
        foreach (var user in users)
        {
            await permissionCache.InvalidateAsync(user.Id, cancellationToken);
        }

        return new BulkUpdateUserPermissionsResultDto
        {
            UpdatedUsersCount = users.Count,
            AffectedUserIds = users.Select(u => u.Id).ToList()
        };
    }
}
