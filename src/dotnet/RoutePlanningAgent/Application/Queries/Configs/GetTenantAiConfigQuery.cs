using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Queries.Configs;

public record GetTenantAiConfigQuery(string Feature) : IRequest<TenantAiConfigDto>;

public class GetTenantAiConfigHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTenantAiConfigQuery, TenantAiConfigDto>
{
    public async Task<TenantAiConfigDto> Handle(
        GetTenantAiConfigQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var config = await context.TenantAiConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Feature == request.Feature, cancellationToken)
            ?? throw new NotFoundException($"TenantAiConfig cho feature '{request.Feature}' chưa được cấu hình");

        return new TenantAiConfigDto
        {
            Id = config.Id,
            TenantId = config.TenantId,
            Feature = config.Feature,
            Policy = config.Policy.ToString(),
            AiProvider = config.AiProvider,
            IsActive = config.IsActive,
            UpdatedAt = config.UpdatedAt
        };
    }
}
