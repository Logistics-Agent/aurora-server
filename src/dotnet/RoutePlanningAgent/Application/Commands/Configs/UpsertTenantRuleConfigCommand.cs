using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Configs;

/// <summary>
/// Upsert ngưỡng rule per-tenant theo (TenantId, RuleName).
/// </summary>
public record UpsertTenantRuleConfigCommand(
    string RuleName,
    bool IsEnabled,
    Dictionary<string, decimal> Thresholds
) : IRequest<TenantRuleConfigDto>;

public class UpsertTenantRuleConfigHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    ITenantRuleConfigService ruleConfigService,
    IOutboxWriter outbox)
    : IRequestHandler<UpsertTenantRuleConfigCommand, TenantRuleConfigDto>
{
    /// <summary>Danh sách rule hợp lệ — khớp Name của 7 rules trong Infrastructure\Rules\Rules.</summary>
    public static readonly IReadOnlySet<string> KnownRuleNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "HeavyWeightRule",
        "LargeVolumeRule",
        "RouteStopCountRule",
        "OnDemandTypeRule",
        "LongDurationRule",
        "MinimumStopsRule",
        "MultiHubRule"
    };

    public async Task<TenantRuleConfigDto> Handle(
        UpsertTenantRuleConfigCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        if (!KnownRuleNames.Contains(request.RuleName))
            throw new DomainException(
                $"RuleName '{request.RuleName}' không hợp lệ. Giá trị cho phép: {string.Join(", ", KnownRuleNames)}");

        foreach (var (key, value) in request.Thresholds)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new DomainException("Threshold key không được để trống");
            if (value < 0)
                throw new DomainException($"Threshold '{key}' phải >= 0");
        }

        var config = await context.TenantRuleConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.RuleName == request.RuleName, cancellationToken);

        if (config is null)
        {
            config = new TenantRuleConfig
            {
                TenantId = tenantId,
                RuleName = request.RuleName
            };
            context.TenantRuleConfigs.Add(config);
        }

        config.IsEnabled = request.IsEnabled;
        config.ThresholdsJson = JsonSerializer.Serialize(request.Thresholds);
        config.UpdatedAt = DateTimeOffset.UtcNow;

        // Outbox: các instance khác invalidate cache qua consumer
        outbox.Enqueue(new TenantRuleConfigChangedEvent
        {
            TenantId = tenantId,
            RuleName = request.RuleName
        });

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate cache local ngay (outbox trễ polling ~10s)
        await ruleConfigService.InvalidateCacheAsync(tenantId, request.RuleName, cancellationToken);

        return new TenantRuleConfigDto
        {
            Id = config.Id,
            TenantId = config.TenantId,
            RuleName = config.RuleName,
            IsEnabled = config.IsEnabled,
            Thresholds = request.Thresholds,
            UpdatedAt = config.UpdatedAt
        };
    }
}
