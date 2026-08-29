using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using IamTenant.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace IamTenant.Application.Commands.Tenants;

public record CreateStaffCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Role = BaseRoleExtensions.StaffCode,
    bool ApplyDefaultPermissions = true,
    List<string>? Permissions = null) : IRequest<StaffDto>;

public class CreateStaffHandler(
    IamTenantDbContext context,
    ICurrentUserService currentUser,
    ICognitoAuthService cognitoService)
    : IRequestHandler<CreateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new ForbiddenException("TenantId is required.");

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == currentUser.TenantId.Value, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        // Validate Email Domain
        if (!request.Email.EndsWith($"@{tenant.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Staff Email must belong to the Company Domain: {tenant.CompanyDomain}");

        var baseRole = BaseRoleExtensions.ParseRole(request.Role);

        // Security Invariant: SYSTEM_ADMIN cannot be assigned within a tenant context
        if (baseRole == BaseRole.SystemAdmin || !baseRole.IsTenantAssignable())
            throw new DomainException("Cannot assign SYSTEM_ADMIN role within a tenant context. Assignable roles: STAFF, MANAGER, TENANT_ADMIN.");

        // Resolve Permissions to provision (Role templates are provisioning seeds only)
        var permissionCodesToAssign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (request.ApplyDefaultPermissions)
        {
            var defaultCodes = baseRole switch
            {
                BaseRole.TenantAdmin => PermissionConstants.GetTenantAdminPermissions(),
                BaseRole.Manager => PermissionConstants.GetDefaultManagerPermissions(),
                BaseRole.Staff => PermissionConstants.GetDefaultStaffPermissions(),
                _ => PermissionConstants.GetDefaultStaffPermissions()
            };

            foreach (var code in defaultCodes)
                permissionCodesToAssign.Add(code);
        }

        if (request.Permissions != null)
        {
            foreach (var code in request.Permissions.Where(p => !string.IsNullOrWhiteSpace(p)))
                permissionCodesToAssign.Add(code.Trim());
        }

        // Security Invariant: Tenant Admin / Staff cannot grant system-only permissions
        var forbiddenSystemPerms = permissionCodesToAssign
            .Where(PermissionConstants.IsSystemOnlyPermission)
            .ToList();

        if (forbiddenSystemPerms.Count > 0)
            throw new DomainException($"Cannot grant platform system-only permissions: {string.Join(", ", forbiddenSystemPerms)}");

        // Validate that requested permissions exist in DB catalog
        var catalogPermissions = await context.Permissions
            .Where(p => permissionCodesToAssign.Contains(p.Code))
            .ToListAsync(cancellationToken);

        var validCodeMap = catalogPermissions.ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);
        var unknownCodes = permissionCodesToAssign.Where(c => !validCodeMap.ContainsKey(c)).ToList();
        if (unknownCodes.Count > 0)
            throw new DomainException($"Unknown permission codes: {string.Join(", ", unknownCodes)}");

        var isAdmin = baseRole == BaseRole.TenantAdmin;

        var staffUser = new User
        {
            TenantId = currentUser.TenantId.Value,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = baseRole,
            Status = Domain.Enums.UserStatus.Invited,
            PermissionVersion = 1
        };

        // Create user in AWS Cognito User Pool
        var targetUserPoolId = (isAdmin ? tenant.AdminUserPoolId : tenant.UserUserPoolId)
            ?? tenant.UserUserPoolId
            ?? tenant.AdminUserPoolId;

        var tempPassword = "TempP@ssw0rd!" + Guid.NewGuid().ToString("N")[..8];

        if (!string.IsNullOrWhiteSpace(targetUserPoolId))
        {
            var cognitoSub = await cognitoService.AdminCreateUserInPoolAsync(
                targetUserPoolId, request.Email, tempPassword, cancellationToken);
            staffUser.CognitoSub = cognitoSub;
        }

        context.Users.Add(staffUser);

        // Attach Direct User Permissions
        foreach (var permCode in permissionCodesToAssign)
        {
            staffUser.UserPermissions.Add(new UserPermission
            {
                UserId = staffUser.Id,
                PermissionId = validCodeMap[permCode],
                TenantId = tenant.Id,
                GrantedByUserId = currentUser.UserId,
                GrantedAt = DateTimeOffset.UtcNow
            });
        }

        // Transactional Outbox: atomically enqueue provisioning event
        var staffCreatedEvent = new TenantStaffCreatedEvent
        {
            TenantId = tenant.Id,
            UserId = staffUser.Id,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
        };

        var outboxMessage = new OutboxMessage
        {
            EventType = nameof(TenantStaffCreatedEvent),
            Payload = JsonSerializer.Serialize(staffCreatedEvent),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.OutboxMessages.Add(outboxMessage);

        // Atomic commit: User + Direct UserPermissions + OutboxMessage
        await context.SaveChangesAsync(cancellationToken);

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            Role = staffUser.Role.ToCode(),
            Permissions = permissionCodesToAssign.OrderBy(p => p).ToList(),
            PermissionVersion = staffUser.PermissionVersion,
            Status = staffUser.Status,
            CreatedAt = staffUser.CreatedAt
        };
    }
}
