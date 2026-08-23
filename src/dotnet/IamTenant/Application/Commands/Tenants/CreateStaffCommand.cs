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
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;
using IamTenant.Domain.Enums;

namespace IamTenant.Application.Commands.Tenants;

public record CreateStaffCommand(
    string Email,
    string FirstName,
    string LastName,
    List<Guid> RoleIds,
    string? StaffType = null) : IRequest<StaffDto>;

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

        // Lấy đúng tenant của người gọi (không lấy "tenant đầu tiên")
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == currentUser.TenantId.Value, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        // Validate Email Domain
        if (!request.Email.EndsWith($"@{tenant.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Staff Email must belong to the Company Domain: {tenant.CompanyDomain}");

        var staffType = ParseStaffType(request.StaffType);

        var roleIds = request.RoleIds.Distinct().ToList();

        // Query roles from DB to determine if any assigned role is TENANT_ADMIN
        var assignedRoles = roleIds.Count > 0
            ? await context.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken)
            : [];

        var isAdmin = assignedRoles.Any(r => r.Code == "TENANT_ADMIN");

        var staffUser = new User
        {
            TenantId = currentUser.TenantId.Value,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = Domain.Enums.UserType.TenantStaff,
            Status = Domain.Enums.UserStatus.Invited,
            StaffType = staffType,
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

        // Save UserRoles mapping
        if (roleIds.Count > 0)
        {
            context.UserRoles.AddRange(roleIds.Select(roleId => new UserRole
            {
                UserId = staffUser.Id,
                RoleId = roleId
            }));
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

        // Atomic commit: User + UserRoles + OutboxMessage
        await context.SaveChangesAsync(cancellationToken);

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            UserType = staffUser.UserType,
            Status = staffUser.Status,
            StaffType = staffUser.StaffType,
            CreatedAt = staffUser.CreatedAt
        };
    }

    internal static Domain.Enums.StaffType ParseStaffType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Domain.Enums.StaffType.Normal;

        if (!Enum.TryParse<Domain.Enums.StaffType>(value, true, out var staffType))
            throw new DomainException(
                $"StaffType '{value}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<Domain.Enums.StaffType>())}");

        return staffType;
    }
}
