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
using Shared.Enums;
using Shared.Exceptions;
using Shared.Security;

namespace IamTenant.Application.Commands.Users;

public record UserRoleResultDto
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
    public int PermissionVersion { get; set; }
    public List<string> ElevatedPermissionsRetained { get; set; } = [];
}

public record UpdateUserRoleCommand(
    Guid UserId,
    string NewRole,
    bool ApplyDefaultPermissions = false) : IRequest<UserRoleResultDto>;

public class UpdateUserRoleHandler(
    IamTenantDbContext context,
    ICurrentUserService currentUser,
    IPermissionCacheService permissionCache)
    : IRequestHandler<UpdateUserRoleCommand, UserRoleResultDto>
{
    private static readonly HashSet<string> ElevatedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        PermissionConstants.RoutePlanning.Approve,
        PermissionConstants.RoutePlanning.Reject,
        PermissionConstants.RoutePlanning.PolicyPublish,
        PermissionConstants.RoutePlanning.PolicyManage,
        PermissionConstants.Mail.ThreadReassign,
        PermissionConstants.Mail.ThreadUnassign,
        PermissionConstants.Mail.QuarantineRelease,
        PermissionConstants.Ocr.Review,
        PermissionConstants.Compliance.Override,
        PermissionConstants.Billing.SettlementManage,
        PermissionConstants.Iam.UserInvite,
        PermissionConstants.Iam.PermissionManage
    };

    public async Task<UserRoleResultDto> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new ForbiddenException("Tenant context is required.");

        var user = await context.Users
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == currentUser.TenantId.Value && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"User '{request.UserId}' not found in tenant.");

        var targetRole = BaseRoleExtensions.ParseRole(request.NewRole);

        // Security Invariant: SYSTEM_ADMIN cannot be assigned within a tenant context
        if (targetRole == BaseRole.SystemAdmin || !targetRole.IsTenantAssignable())
            throw new DomainException("Cannot assign SYSTEM_ADMIN role within a tenant context. Assignable roles: STAFF, MANAGER, TENANT_ADMIN.");

        var oldRole = user.Role;
        user.Role = targetRole;

        // Apply Defaults if requested (UNION with existing permissions, never overwrite)
        if (request.ApplyDefaultPermissions)
        {
            var defaultCodes = targetRole switch
            {
                BaseRole.TenantAdmin => PermissionConstants.GetTenantAdminPermissions(),
                BaseRole.Manager => PermissionConstants.GetDefaultManagerPermissions(),
                BaseRole.Staff => PermissionConstants.GetDefaultStaffPermissions(),
                _ => PermissionConstants.GetDefaultStaffPermissions()
            };

            var existingCodes = user.UserPermissions
                .Where(up => up.Permission != null)
                .Select(up => up.Permission!.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingCodes = defaultCodes
                .Where(code => !existingCodes.Contains(code) && !PermissionConstants.IsSystemOnlyPermission(code))
                .ToList();

            if (missingCodes.Count > 0)
            {
                var permissionsToAdd = await context.Permissions
                    .Where(p => missingCodes.Contains(p.Code))
                    .ToListAsync(cancellationToken);

                foreach (var perm in permissionsToAdd)
                {
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

        // On Downgrade (e.g. MANAGER -> STAFF or TENANT_ADMIN -> STAFF): detect elevated permissions retained
        var elevatedRetained = new List<string>();
        if (oldRole > targetRole)
        {
            var currentCodes = user.UserPermissions
                .Where(up => up.Permission != null)
                .Select(up => up.Permission!.Code);

            elevatedRetained = currentCodes
                .Where(code => ElevatedPermissions.Contains(code))
                .OrderBy(c => c)
                .ToList();
        }

        user.PermissionVersion++;

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Redis cache
        await permissionCache.InvalidateAsync(user.Id, cancellationToken);

        var finalPermissions = user.UserPermissions
            .Where(up => up.Permission != null)
            .Select(up => up.Permission!.Code)
            .OrderBy(p => p)
            .ToList();

        return new UserRoleResultDto
        {
            UserId = user.Id,
            Role = user.Role.ToCode(),
            Permissions = finalPermissions,
            PermissionVersion = user.PermissionVersion,
            ElevatedPermissionsRetained = elevatedRetained
        };
    }
}
