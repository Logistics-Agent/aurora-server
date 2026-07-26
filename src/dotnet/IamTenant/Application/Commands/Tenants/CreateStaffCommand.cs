using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using IamTenant.Application.Interfaces;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using Shared.Security;
using IamTenant.Domain.Enums;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>
/// TenantId không cần truyền vào — được resolve từ ICurrentUserService.
/// </summary>
public record CreateStaffCommand(
    string Email,
    string FirstName,
    string LastName,
    List<Guid> RoleIds) : IRequest<StaffDto>;

public class CreateStaffHandler(
    IamTenantDbContext context,
    IPublishEndpoint publishEndpoint,
    ICurrentUserService currentUser,
    ICognitoAuthService cognitoService)
    : IRequestHandler<CreateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException("TenantId is required.");

        var tenant = await context.Tenants.FirstOrDefaultAsync(cancellationToken: cancellationToken)
            ?? throw new Exception("Tenant not found.");

        // Validate Email Domain
        if (!request.Email.EndsWith($"@{tenant.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Staff Email must belong to the Company Domain: {tenant.CompanyDomain}");

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
            UserType = isAdmin ? UserType.TenantAdmin : UserType.TenantStaff,
            Status = UserStatus.Invited,
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
            context.UserRoles.Add(new UserRole
            {
                UserId = staffUser.Id,
                RoleIds = roleIds
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        // Publish invitation event
        await publishEndpoint.Publish(new TenantStaffCreatedEvent
        {
            TenantId = tenant.Id,
            UserId = staffUser.Id,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
        }, cancellationToken);

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
}
