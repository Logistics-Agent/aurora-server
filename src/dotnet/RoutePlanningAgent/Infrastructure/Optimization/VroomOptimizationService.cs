using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using Shared.Exceptions;
using Route = RoutePlanningAgent.Domain.Route; // tránh nhầm với Microsoft.AspNetCore.Routing.Route

namespace RoutePlanningAgent.Infrastructure.Optimization;

/// <summary>
/// Gọi VROOM (http POST /) để giải bài toán VRP; VROOM dùng OSRM (MLD) làm routing engine
/// để tính ma trận thời gian di chuyển thực tế.
///
/// Quy ước mapping:
/// - Stop có Sequence nhỏ nhất = điểm xuất phát (vehicle.start)
/// - Các stop còn lại = jobs — VROOM tự tối ưu thứ tự thăm
/// - VROOM tọa độ dạng [longitude, latitude]; thời gian tính bằng GIÂY
/// </summary>
public class VroomOptimizationService(
    HttpClient httpClient,
    ILogger<VroomOptimizationService> logger) : IRouteOptimizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RouteOptimizationResult> OptimizeAsync(Route route, CancellationToken ct = default)
    {
        var orderedStops = route.Stops.OrderBy(s => s.Sequence).ToList();
        if (orderedStops.Count < 2)
            throw new DomainException("Route phải có tối thiểu 2 điểm dừng để tối ưu");

        var startStop = orderedStops[0];
        var jobStops = orderedStops.Skip(1).ToList();

        // VROOM yêu cầu id kiểu số — map job id (1-based) → RouteStop
        var jobIdToStop = new Dictionary<int, RouteStop>();
        var jobs = new List<object>();
        for (var i = 0; i < jobStops.Count; i++)
        {
            var jobId = i + 1;
            jobIdToStop[jobId] = jobStops[i];
            jobs.Add(new
            {
                id = jobId,
                location = new[] { jobStops[i].Longitude, jobStops[i].Latitude },
                service = jobStops[i].ServiceDurationMinutes * 60 // giây
            });
        }

        var request = new
        {
            vehicles = new[]
            {
                new
                {
                    id = 1,
                    profile = "car",
                    start = new[] { startStop.Longitude, startStop.Latitude }
                }
            },
            jobs,
            // g = true: yêu cầu geometry để VROOM trả về distance (mét) từ OSRM
            options = new { g = true }
        };

        VroomResponse? vroom;
        try
        {
            using var response = await httpClient.PostAsJsonAsync("/", request, JsonOptions, ct);
            response.EnsureSuccessStatusCode();
            vroom = await response.Content.ReadFromJsonAsync<VroomResponse>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "VROOM optimization call failed for route {RouteId}", route.Id);
            throw new DomainException("Optimization service (VROOM/OSRM) hiện không khả dụng — thử lại sau");
        }

        if (vroom is null || vroom.Code != 0)
            throw new DomainException($"VROOM solver trả về lỗi (code={vroom?.Code}): {vroom?.Error}");

        if (vroom.Unassigned is { Count: > 0 })
        {
            var names = vroom.Unassigned
                .Select(u => jobIdToStop.TryGetValue(u.Id, out var s) ? s.LocationName : u.Id.ToString());
            throw new DomainException(
                $"Không thể xếp lịch cho các điểm dừng: {string.Join(", ", names)} (ngoài vùng bản đồ?)");
        }

        var vroomRoute = vroom.Routes.FirstOrDefault()
            ?? throw new DomainException("VROOM không trả về route nào");

        // Dựng thứ tự mới: start stop giữ Sequence 1, jobs theo thứ tự steps
        var optimized = new List<OptimizedStop>
        {
            new(startStop.Id, 1, 0)
        };

        var sequence = 2;
        var totalDurationSeconds = 0L;
        foreach (var step in vroomRoute.Steps)
        {
            if (step.Type == "job" && step.Id.HasValue && jobIdToStop.TryGetValue(step.Id.Value, out var stop))
            {
                optimized.Add(new OptimizedStop(
                    stop.Id,
                    sequence++,
                    (int)Math.Round(step.Arrival / 60.0)));
            }

            totalDurationSeconds = Math.Max(totalDurationSeconds, step.Arrival + step.Service);
        }

        return new RouteOptimizationResult
        {
            Stops = optimized,
            TotalDistanceKm = Math.Round((decimal)vroomRoute.Distance / 1000m, 3),
            TotalDurationMinutes = (int)Math.Round(totalDurationSeconds / 60.0)
        };
    }

    // ===== VROOM response contract (rút gọn — chỉ các field cần dùng) =====

    private sealed record VroomResponse
    {
        public int Code { get; init; }
        public string? Error { get; init; }
        public List<VroomUnassigned> Unassigned { get; init; } = [];
        public List<VroomRoute> Routes { get; init; } = [];
    }

    private sealed record VroomUnassigned
    {
        public int Id { get; init; }
    }

    private sealed record VroomRoute
    {
        public long Distance { get; init; } // mét (có khi options.g = true)
        public long Duration { get; init; } // giây (chỉ thời gian di chuyển)
        public List<VroomStep> Steps { get; init; } = [];
    }

    private sealed record VroomStep
    {
        public string Type { get; init; } = string.Empty; // start | job | end
        public int? Id { get; init; }
        public long Arrival { get; init; } // giây kể từ lúc xuất phát
        public long Service { get; init; }
    }
}
