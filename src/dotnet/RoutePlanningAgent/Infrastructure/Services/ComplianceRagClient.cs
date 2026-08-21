using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RegulatoryComplianceRag.Grpc;

namespace RoutePlanningAgent.Infrastructure.Services;

public class ComplianceRagClient(
    ComplianceRag.ComplianceRagClient grpcClient)
    : IComplianceRagService
{
    public async Task<ComplianceCheckResultDto> CheckComplianceAsync(
        Guid tenantId,
        string routeJson,
        IReadOnlyList<string> riskReasons,
        CancellationToken ct = default)
    {
        var request = new CheckRouteComplianceRequest
        {
            TenantId = tenantId.ToString(),
            RouteJson = routeJson
        };
        request.RiskReasons.AddRange(riskReasons);

        var response = await grpcClient.CheckRouteComplianceAsync(request, cancellationToken: ct);

        return new ComplianceCheckResultDto
        {
            HasViolations = response.HasViolations,
            ViolationSummaries = [.. response.ViolationSummaries],
            DocumentRefs = [.. response.DocumentRefs],
            ApplicableRegulations = [.. response.ApplicableRegulations],
            MergedContext = response.MergedContext,
            RequiresHumanApproval = response.RequiresHumanApproval
        };
    }
}
