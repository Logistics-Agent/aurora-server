using IamTenant.Application.Interfaces;
using IamTenant.Domain.Enums;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace IamTenant.Application.Queries.Auth;

public record ResolveTenantAuthClientQuery(string TenantCode, string UserType) : IRequest<string>;

public class ResolveTenantAuthClientQueryHandler(IamTenantDbContext context) : IRequestHandler<ResolveTenantAuthClientQuery, string>
{
    public async Task<string> Handle(ResolveTenantAuthClientQuery request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim();
        if (string.IsNullOrWhiteSpace(tenantCode))
            throw new Shared.Exceptions.DomainException("Tenant code is required.");

        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == tenantCode && !t.IsDeleted, cancellationToken)
            ?? throw new Shared.Exceptions.NotFoundException("Tenant not found.");

        if (tenant.Status != TenantStatus.Active)
            throw new Shared.Exceptions.ForbiddenException("Tenant is suspended.");

        BaseRoleExtensions.TryParseRole(request.UserType, out var baseRole);

        var clientId = baseRole switch
        {
            Shared.Enums.BaseRole.TenantAdmin => tenant.AdminUserPoolClientId,
            _ => tenant.StaffUserPoolClientId
        };

        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = tenant.AdminUserPoolClientId ?? tenant.StaffUserPoolClientId;
        }

        return !string.IsNullOrWhiteSpace(clientId)
            ? clientId
            : throw new Shared.Exceptions.NotFoundException("Tenant auth client is not configured.");
    }
}