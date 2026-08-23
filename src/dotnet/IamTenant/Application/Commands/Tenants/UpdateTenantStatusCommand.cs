using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>
/// Cập nhật trạng thái tenant (Active/Suspended/...) — tách riêng khỏi UpdateTenantCommand
/// (trước đây UpdateTenantStatus RPC map nhầm sang UpdateTenantCommand và bỏ qua status).
/// </summary>
public record UpdateTenantStatusCommand(Guid Id, string Status) : IRequest<TenantDto>;

public class UpdateTenantStatusHandler(IamTenantDbContext context)
    : IRequestHandler<UpdateTenantStatusCommand, TenantDto>
{
    public async Task<TenantDto> Handle(UpdateTenantStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Domain.Enums.TenantStatus>(request.Status, true, out var status))
            throw new DomainException(
                $"TenantStatus '{request.Status}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<Domain.Enums.TenantStatus>())}");

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Tenant not found");

        tenant.Status = status;

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
