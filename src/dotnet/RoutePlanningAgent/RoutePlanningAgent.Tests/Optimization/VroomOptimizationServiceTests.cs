using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RoutePlanningAgent.Infrastructure.Optimization;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Exceptions;
using Xunit;

namespace RoutePlanningAgent.Tests.Optimization;

public class VroomOptimizationServiceTests
{
    private static VroomOptimizationService Create(HttpResponseMessage response, out FakeHandler handler)
    {
        handler = new FakeHandler(response);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        return new VroomOptimizationService(client, NullLogger<VroomOptimizationService>.Instance);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task Optimize_ParseResponse_ReorderTheoSteps()
    {
        // Route 3 stops: stop đầu = start; 2 stops còn lại là job 1 và job 2.
        // VROOM trả thứ tự: job 2 trước, job 1 sau.
        var route = RouteBuilder.Build(stopCount: 3);
        var orderedStops = route.Stops.OrderBy(s => s.Sequence).ToList();

        const string vroomResponse = """
        {
          "code": 0,
          "unassigned": [],
          "routes": [
            {
              "distance": 42500,
              "duration": 3600,
              "steps": [
                { "type": "start", "arrival": 0, "service": 0 },
                { "type": "job", "id": 2, "arrival": 1500, "service": 600 },
                { "type": "job", "id": 1, "arrival": 3300, "service": 600 },
                { "type": "end", "arrival": 3900, "service": 0 }
              ]
            }
          ]
        }
        """;

        var service = Create(Json(vroomResponse), out var handler);
        var result = await service.OptimizeAsync(route);

        // Request gửi đi: 1 vehicle + 2 jobs + options.g = true
        Assert.Contains("\"vehicles\"", handler.LastRequestBody);
        Assert.Contains("\"g\":true", handler.LastRequestBody);

        // Start stop giữ Sequence 1
        Assert.Equal(orderedStops[0].Id, result.Stops[0].StopId);
        Assert.Equal(1, result.Stops[0].Sequence);

        // Job 2 (= stop thứ 3 ban đầu) đứng Sequence 2, ETA 25 phút
        Assert.Equal(orderedStops[2].Id, result.Stops[1].StopId);
        Assert.Equal(2, result.Stops[1].Sequence);
        Assert.Equal(25, result.Stops[1].EstimatedArrivalMinutes);

        // Job 1 (= stop thứ 2 ban đầu) đứng Sequence 3
        Assert.Equal(orderedStops[1].Id, result.Stops[2].StopId);
        Assert.Equal(3, result.Stops[2].Sequence);

        // Totals: 42500m → 42.5km; end arrival 3900s → 65 phút
        Assert.Equal(42.5m, result.TotalDistanceKm);
        Assert.Equal(65, result.TotalDurationMinutes);
        Assert.Equal("VROOM", result.Provider);
        Assert.Equal("OSRM-MLD", result.Model);
    }

    [Fact]
    public async Task Optimize_CoUnassigned_DomainExceptionKemTenStop()
    {
        var route = RouteBuilder.Build(stopCount: 3);

        const string vroomResponse = """
        { "code": 0, "unassigned": [ { "id": 1 } ], "routes": [] }
        """;

        var service = Create(Json(vroomResponse), out _);
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.OptimizeAsync(route));
        Assert.Contains("Stop 2", ex.Message); // job 1 = stop thứ 2 ban đầu
    }

    [Fact]
    public async Task Optimize_VroomLoi_DomainException()
    {
        var route = RouteBuilder.Build(stopCount: 3);

        const string vroomResponse = """{ "code": 1, "error": "Internal error" }""";

        var service = Create(Json(vroomResponse), out _);
        await Assert.ThrowsAsync<DomainException>(() => service.OptimizeAsync(route));
    }

    [Fact]
    public async Task Optimize_HttpLoi_DomainException()
    {
        var route = RouteBuilder.Build(stopCount: 3);

        var service = Create(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), out _);
        await Assert.ThrowsAsync<DomainException>(() => service.OptimizeAsync(route));
    }

    [Fact]
    public async Task Optimize_ItHon2Stops_DomainException()
    {
        var route = RouteBuilder.Build(stopCount: 1);

        var service = Create(Json("{}"), out _);
        await Assert.ThrowsAsync<DomainException>(() => service.OptimizeAsync(route));
    }

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
