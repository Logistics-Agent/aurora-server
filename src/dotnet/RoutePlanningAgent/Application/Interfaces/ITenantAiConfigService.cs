using System;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Domain;
using Shared.Enums;

namespace RoutePlanningAgent.Application.Interfaces;

public interface ITenantAiConfigService
{
    Task<TenantAiConfig?> GetConfigAsync(Guid tenantId, string feature, CancellationToken ct = default);
    Task<AutomationPolicy> GetPolicyAsync(Guid tenantId, string feature, CancellationToken ct = default);
    Task InvalidateCacheAsync(Guid tenantId, string feature, CancellationToken ct = default);
}
