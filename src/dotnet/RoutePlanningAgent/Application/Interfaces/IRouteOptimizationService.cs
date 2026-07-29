using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Route = RoutePlanningAgent.Domain.Route; // tránh nhầm với Microsoft.AspNetCore.Routing.Route

namespace RoutePlanningAgent.Application.Interfaces;

/// <summary>
/// Solver tối ưu thứ tự điểm dừng + tính khoảng cách/thời gian thực tế.
/// Hiện thực bằng VROOM (VRP solver) + OSRM (routing engine, thuật toán MLD).
/// </summary>
public interface IRouteOptimizationService
{
    Task<RouteOptimizationResult> OptimizeAsync(Route route, CancellationToken ct = default);
}

/// <summary>Kết quả tối ưu: thứ tự stop mới + ETA từng stop + tổng quãng đường/thời gian.</summary>
public record RouteOptimizationResult
{
    /// <summary>Danh sách stop theo thứ tự tối ưu (Sequence mới 1..n).</summary>
    public required List<OptimizedStop> Stops { get; init; }

    public decimal TotalDistanceKm { get; init; }
    public int TotalDurationMinutes { get; init; }
    public string Provider { get; init; } = "VROOM";
    public string Model { get; init; } = "OSRM-MLD";
}

public record OptimizedStop(Guid StopId, int Sequence, int EstimatedArrivalMinutes);
