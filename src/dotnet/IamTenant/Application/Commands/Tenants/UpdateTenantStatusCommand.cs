using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using IamTenant.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Tenants;

public record UpdateTenantStatusCommand(Guid Id, TenantStatus Status) : IRequest<TenantDto>;

public class UpdateTenantStatusHandler(IamTenantDbContext context) : IRequestHandler<UpdateTenantStatusCommand, TenantDto>
{
    public async Task<TenantDto> Handle(UpdateTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken)
            ?? throw new Exception("Tenant not found");

        tenant.Status = request.Status;

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
}
