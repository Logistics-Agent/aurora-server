using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Configs;

public record UpsertTenantAiConfigCommand(
    string Feature,
    string Policy,
    string AiProvider,
    bool IsActive
) : IRequest<TenantAiConfigDto>;

public class UpsertTenantAiConfigHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<UpsertTenantAiConfigCommand, TenantAiConfigDto>
{
    public async Task<TenantAiConfigDto> Handle(
        UpsertTenantAiConfigCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var config = await context.TenantRiskPolicyConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (config == null)
        {
            config = new TenantRiskPolicyConfig
            {
                TenantId = tenantId,
                PolicyMode = request.Policy.Equals("Manual", StringComparison.OrdinalIgnoreCase)
                    ? RiskPolicyMode.UsePlatformDefault
                    : RiskPolicyMode.UseCustomPolicy,
                ActivePolicyId = request.Policy,
                ActivePolicyVersion = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.TenantRiskPolicyConfigs.Add(config);
        }
        else
        {
            config.PolicyMode = request.Policy.Equals("Manual", StringComparison.OrdinalIgnoreCase)
                ? RiskPolicyMode.UsePlatformDefault
                : RiskPolicyMode.UseCustomPolicy;
            config.ActivePolicyId = request.Policy;
            config.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        return new TenantAiConfigDto
        {
            Id = config.Id,
            TenantId = config.TenantId,
            Feature = request.Feature,
            Policy = request.Policy,
            AiProvider = string.IsNullOrWhiteSpace(request.AiProvider) ? "Gemini" : request.AiProvider,
            IsActive = request.IsActive,
            UpdatedAt = config.UpdatedAt
        };
    }
}
