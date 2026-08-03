using System.Text.Json;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using IamTenant.Application.Interfaces;
using IamTenant.Application.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using IamTenant.Domain.Enums;

namespace IamTenant.Application.Commands.Tenants;

public record CreateTenantCommand(string Name, string CompanyDomain, string AdminEmail, Guid IdempotencyKey, string? TaxCode = null, PlanType PlanType = PlanType.Standard) : IRequest<TenantDto>;

public class CreateTenantHandler(
    IamTenantDbContext context,
    ICognitoAuthService cognitoService,
    IAuditTrailService auditTrail) : IRequestHandler<CreateTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // 0. Idempotency Check
        var existing = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            
        if (existing is not null)
        {
            return new TenantDto
            {
                Id = existing.Id,
                Code = existing.Code,
                Name = existing.Name,
                TaxCode = existing.TaxCode,
                CompanyDomain = existing.CompanyDomain,
                PlanType = existing.PlanType,
                Status = existing.Status,
                CreatedAt = existing.CreatedAt
            };
        }

        // 1. Check if CompanyDomain is already used
        if (await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.CompanyDomain == request.CompanyDomain && !t.IsDeleted, cancellationToken))
        {
            throw new Shared.Exceptions.ConflictException("Company Domain already exists.");
        }

        // 2. Validate AdminEmail belongs to CompanyDomain
        if (!request.AdminEmail.EndsWith($"@{request.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
        {
            throw new Shared.Exceptions.DomainException("Admin Email must belong to the Company Domain.");
        }

        // 3. Create Tenant
        var tenant = Tenant.Create(request.Name, request.CompanyDomain, request.TaxCode, request.PlanType, request.IdempotencyKey);
        var tempPassword = GenerateTempPassword();

        // Create Admin & User Cognito User Pools per Tenant Code
        var pools = await cognitoService.CreateTenantUserPoolsAsync(tenant.Code, cancellationToken);
        tenant.AdminUserPoolId = pools.AdminUserPoolId;
        tenant.AdminUserPoolClientId = pools.AdminUserPoolClientId;
        tenant.UserUserPoolId = pools.UserUserPoolId;
        tenant.UserUserPoolClientId = pools.UserUserPoolClientId;

        context.Tenants.Add(tenant);

        var adminUser = new User
        {
            TenantId = tenant.Id,
            Email = request.AdminEmail,
            UserType = UserType.TenantAdmin,
            Status = UserStatus.Invited,
        };

        // Cognito AdminCreateUser in the newly provisioned Admin User Pool
        var cognitoSub = await cognitoService.AdminCreateUserInPoolAsync(tenant.AdminUserPoolId, request.AdminEmail, tempPassword, cancellationToken);
        adminUser.CognitoSub = cognitoSub;

        context.Users.Add(adminUser);

        // 5. Publish Event via Outbox
        var tenantAdminCreatedEvent = new TenantAdminCreatedEvent
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            UserId = adminUser.Id,
            Email = adminUser.Email
        };

        var outboxMessage = new OutboxMessage
        {
            EventType = nameof(TenantAdminCreatedEvent),
            Payload = JsonSerializer.Serialize(tenantAdminCreatedEvent),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.OutboxMessages.Add(outboxMessage);

        // 6. Audit Logging
        await auditTrail.LogAsync(
            AuditActions.CreateTenant,
            $"Tenant: {tenant.Id}",
            new { tenant.Name, tenant.CompanyDomain, request.AdminEmail },
            cancellationToken);

        // 7. Save changes
        await context.SaveChangesAsync(cancellationToken);

        return new TenantDto
        {
            Id = tenant.Id,
            Code = tenant.Code,
            Name = tenant.Name,
            TaxCode = tenant.TaxCode,
            CompanyDomain = tenant.CompanyDomain,
            PlanType = tenant.PlanType,
            Status = tenant.Status,
            CreatedAt = tenant.CreatedAt
        };
    }

    private static string GenerateTempPassword()
    {
        return "TempP@ssw0rd!" + Guid.NewGuid().ToString("N")[..8];
    }
}
