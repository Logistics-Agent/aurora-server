using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Validation;
using RoutePlanningAgent.Domain.Enums;
using Shared.Exceptions;
using Xunit;

namespace RoutePlanningAgent.Tests.Validation;

public class RouteValidatorTests
{
    private static List<RouteStopInputDto> ValidStops(int count = 2)
    {
        var stops = new List<RouteStopInputDto>();
        for (var i = 1; i <= count; i++)
        {
            stops.Add(new RouteStopInputDto
            {
                Sequence = i,
                StopType = "Pickup",
                LocationName = $"Stop {i}",
                Address = $"Địa chỉ {i}",
                Latitude = 10.7,
                Longitude = 106.6,
                EstimatedArrivalMinutes = 0,
                ServiceDurationMinutes = 5
            });
        }
        return stops;
    }

    private static void Validate(
        string name = "Route A",
        decimal weight = 100, decimal volume = 10, decimal distance = 0, int duration = 0,
        List<RouteStopInputDto>? stops = null)
        => RouteValidator.Validate(name, weight, volume, distance, duration, stops ?? ValidStops());

    [Fact]
    public void InputHopLe_KhongThrow() => Validate();

    [Fact]
    public void TenRong_Throw() => Assert.Throws<DomainException>(() => Validate(name: "  "));

    [Fact]
    public void TenQuaDai_Throw() => Assert.Throws<DomainException>(() => Validate(name: new string('a', 201)));

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void SoLieuAm_Throw(decimal weight, decimal volume, decimal distance, int duration)
        => Assert.Throws<DomainException>(() => Validate(weight: weight, volume: volume, distance: distance, duration: duration));

    [Fact]
    public void ItHon2Stops_Throw()
        => Assert.Throws<DomainException>(() => Validate(stops: ValidStops(1)));

    [Fact]
    public void SequenceTrung_Throw()
    {
        var stops = ValidStops(2);
        stops[1] = stops[1] with { Sequence = 1 };
        Assert.Throws<DomainException>(() => Validate(stops: stops));
    }

    [Fact]
    public void SequenceAm_Throw()
    {
        var stops = ValidStops(2);
        stops[0] = stops[0] with { Sequence = -1 };
        Assert.Throws<DomainException>(() => Validate(stops: stops));
    }

    [Theory]
    [InlineData(91, 106.6)]
    [InlineData(-91, 106.6)]
    [InlineData(10.7, 181)]
    [InlineData(10.7, -181)]
    public void LatLongNgoaiKhoang_Throw(double lat, double lng)
    {
        var stops = ValidStops(2);
        stops[0] = stops[0] with { Latitude = lat, Longitude = lng };
        Assert.Throws<DomainException>(() => Validate(stops: stops));
    }

    [Fact]
    public void StopTypeSai_Throw_KhongFallback()
    {
        var stops = ValidStops(2);
        stops[0] = stops[0] with { StopType = "KhongTonTai" };
        Assert.Throws<DomainException>(() => Validate(stops: stops));
    }

    [Fact]
    public void ParseRouteType_Sai_Throw()
        => Assert.Throws<DomainException>(() => RouteValidator.ParseRouteType("KhongTonTai"));

    [Fact]
    public void ParseRouteType_KhongPhanBietHoaThuong()
        => Assert.Equal(RouteType.OnDemand, RouteValidator.ParseRouteType("ondemand"));
}
