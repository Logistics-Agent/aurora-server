using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Application.DTOs.Routes;

namespace RoutePlanningAgent.Application.Interfaces;

public interface IComplianceRagService
{
    Task<ComplianceCheckResultDto> CheckComplianceAsync(
        Guid tenantId,
        string routeJson,
        IReadOnlyList<string> riskReasons,
        CancellationToken ct = default);
}
