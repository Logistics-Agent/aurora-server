using System;
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

        var config = await context.TenantRiskPolicyConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (config == null)
        {
            return new TenantAiConfigDto
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Feature = request.Feature,
                Policy = "RulesAndLlm",
                AiProvider = "Gemini",
                IsActive = true,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        return new TenantAiConfigDto
        {
            Id = config.Id,
            TenantId = config.TenantId,
            Feature = request.Feature,
            Policy = !string.IsNullOrWhiteSpace(config.ActivePolicyId) ? config.ActivePolicyId : "RulesAndLlm",
            AiProvider = "Gemini",
            IsActive = true,
            UpdatedAt = config.UpdatedAt
        };
    }
}
