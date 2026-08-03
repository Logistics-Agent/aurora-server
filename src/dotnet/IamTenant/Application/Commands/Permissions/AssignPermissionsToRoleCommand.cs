using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IamTenant.Application.Constants;
using System.Text.Json;
using Shared.Events;

namespace IamTenant.Application.Commands.Permissions;

// ─────────────────────────────────────────────────────────────────────────────
// ASSIGN PERMISSIONS TO ROLE
// Sau khi assign, tăng permission_version cho tất cả user có role này
// và invalidate Redis cache của họ bất đồng bộ qua Outbox.
// ─────────────────────────────────────────────────────────────────────────────
public record AssignPermissionsToRoleCommand(Guid RoleId, List<Guid> PermissionIds) : IRequest<RoleDto>;

public class AssignPermissionsToRoleHandler(
    IamTenantDbContext context,
    Interfaces.IAuditTrailService auditTrail)
    : IRequestHandler<AssignPermissionsToRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(AssignPermissionsToRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new Shared.Exceptions.NotFoundException("Role not found.");

        var validIds = await context.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var invalidIds = request.PermissionIds.Except(validIds).ToList();
        if (invalidIds.Any())
            throw new Shared.Exceptions.NotFoundException($"Permissions not found: {string.Join(", ", invalidIds)}");

        var currentPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();

        // Calculate differences to avoid unnecessary DB operations, version increments, and cache clears
        var toRemove = role.RolePermissions.Where(rp => !validIds.Contains(rp.PermissionId)).ToList();
        var toAdd = validIds.Except(currentPermissionIds).ToList();

        if (toRemove.Any() || toAdd.Any())
        {
            // Remove obsolete permissions
            foreach (var rp in toRemove)
            {
                role.RolePermissions.Remove(rp);
            }

            // Add new permissions
            foreach (var permId in toAdd)
            {
                role.RolePermissions.Add(new Domain.RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permId
                });
            }

            // Enqueue Outbox Message to asynchronously update user versions and cache
            var rolePermissionsChangedEvent = new RolePermissionsChangedEvent
            {
                RoleId = role.Id
            };

            context.OutboxMessages.Add(new Domain.OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                EventType = nameof(RolePermissionsChangedEvent),
                Payload = JsonSerializer.Serialize(rolePermissionsChangedEvent),
                CreatedAt = DateTimeOffset.UtcNow
            });

            await auditTrail.LogAsync(
                AuditActions.AssignPermission,
                $"Role: {role.Id}",
                new { AssignedPermissions = request.PermissionIds },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        return new RoleDto
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            PermissionIds = role.RolePermissions.Select(rp => rp.PermissionId.ToString()).ToList(),
            CreatedAt = role.CreatedAt
        };
    }
}
