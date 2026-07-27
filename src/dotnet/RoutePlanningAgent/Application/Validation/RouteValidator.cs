using System;
using System.Collections.Generic;
using System.Linq;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Domain.Enums;
using Shared.Exceptions;

namespace RoutePlanningAgent.Application.Validation;

/// <summary>
/// Validate input tạo/sửa Route. Sai → DomainException (ExceptionInterceptor map thành InvalidArgument).
/// Parse enum ở chế độ strict — KHÔNG silent-fallback về Fixed/Pickup.
/// </summary>
public static class RouteValidator
{
    public const int MinStops = 2; // tối thiểu điểm đi + điểm đến

    public static RouteType ParseRouteType(string value)
    {
        if (!Enum.TryParse<RouteType>(value, true, out var routeType))
            throw new DomainException(
                $"RouteType '{value}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<RouteType>())}");
        return routeType;
    }

    public static StopType ParseStopType(string value)
    {
        if (!Enum.TryParse<StopType>(value, true, out var stopType))
            throw new DomainException(
                $"StopType '{value}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<StopType>())}");
        return stopType;
    }

    public static void Validate(
        string name,
        decimal maxWeightKg,
        decimal maxVolumeM3,
        decimal estimatedDistanceKm,
        int estimatedDurationMinutes,
        IReadOnlyList<RouteStopInputDto> stops)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tên route không được để trống");
        if (name.Length > 200)
            throw new DomainException("Tên route không được vượt quá 200 ký tự");

        if (maxWeightKg < 0) throw new DomainException("MaxWeightKg phải >= 0");
        if (maxVolumeM3 < 0) throw new DomainException("MaxVolumeM3 phải >= 0");
        if (estimatedDistanceKm < 0) throw new DomainException("EstimatedDistanceKm phải >= 0");
        if (estimatedDurationMinutes < 0) throw new DomainException("EstimatedDurationMinutes phải >= 0");

        if (stops is null || stops.Count < MinStops)
            throw new DomainException($"Route phải có tối thiểu {MinStops} điểm dừng (điểm đi + điểm đến)");

        var sequences = new HashSet<int>();
        foreach (var stop in stops)
        {
            if (stop.Sequence <= 0)
                throw new DomainException($"Sequence của stop '{stop.LocationName}' phải là số dương");
            if (!sequences.Add(stop.Sequence))
                throw new DomainException($"Sequence {stop.Sequence} bị trùng lặp");

            if (string.IsNullOrWhiteSpace(stop.LocationName))
                throw new DomainException("LocationName không được để trống");
            if (string.IsNullOrWhiteSpace(stop.Address))
                throw new DomainException($"Address của stop '{stop.LocationName}' không được để trống");

            if (stop.Latitude is < -90 or > 90)
                throw new DomainException($"Latitude của stop '{stop.LocationName}' phải trong khoảng [-90, 90]");
            if (stop.Longitude is < -180 or > 180)
                throw new DomainException($"Longitude của stop '{stop.LocationName}' phải trong khoảng [-180, 180]");

            if (stop.EstimatedArrivalMinutes < 0)
                throw new DomainException($"EstimatedArrivalMinutes của stop '{stop.LocationName}' phải >= 0");
            if (stop.ServiceDurationMinutes < 0)
                throw new DomainException($"ServiceDurationMinutes của stop '{stop.LocationName}' phải >= 0");

            // Validate StopType strict (throw nếu sai)
            ParseStopType(stop.StopType);
        }
    }
}
