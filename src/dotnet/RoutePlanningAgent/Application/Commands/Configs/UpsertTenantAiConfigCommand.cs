using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Configs;

/// <summary>
/// Upsert cấu hình AI automation cho tenant theo (TenantId, Feature).
/// Đây là write-path duy nhất để bật các nhánh LLM/Approval (mặc định không có row = RulesOnly).
/// </summary>
public record UpsertTenantAiConfigCommand(
    string Feature,
    string Policy,
    string AiProvider,
    bool IsActive
) : IRequest<TenantAiConfigDto>;

public class UpsertTenantAiConfigHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    ITenantAiConfigService aiConfigService,
    IOutboxWriter outbox)
    : IRequestHandler<UpsertTenantAiConfigCommand, TenantAiConfigDto>
{
    private static readonly HashSet<string> AllowedProviders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Gemini",      // gói Standard
            "AzureOpenAI"  // gói Enterprise
        };

    public async Task<TenantAiConfigDto> Handle(
        UpsertTenantAiConfigCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        if (string.IsNullOrWhiteSpace(request.Feature))
            throw new DomainException("Feature không được để trống");

        if (!Enum.TryParse<AutomationPolicy>(request.Policy, true, out var policy))
            throw new DomainException(
                $"Policy '{request.Policy}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<AutomationPolicy>())}");

        if (!AllowedProviders.Contains(request.AiProvider))
            throw new DomainException(
                $"AiProvider '{request.AiProvider}' không hợp lệ. Giá trị cho phép: Gemini, AzureOpenAI");

        var config = await context.TenantAiConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Feature == request.Feature, cancellationToken);

        if (config is null)
        {
            config = new TenantAiConfig
            {
                TenantId = tenantId,
                Feature = request.Feature
            };
            context.TenantAiConfigs.Add(config);
        }

        config.Policy = policy;
        config.AiProvider = request.AiProvider;
        config.IsActive = request.IsActive;
        config.UpdatedAt = DateTimeOffset.UtcNow;

        // Outbox: các instance khác invalidate cache qua consumer
        outbox.Enqueue(new TenantAiConfigChangedEvent
        {
            TenantId = tenantId,
            Feature = request.Feature
        });

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate cache local NGAY (outbox có độ trễ polling ~10s)
        await aiConfigService.InvalidateCacheAsync(tenantId, request.Feature, cancellationToken);

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
