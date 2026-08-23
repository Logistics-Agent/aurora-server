using RoutePlanningAgent.Infrastructure.AI;
using Xunit;

namespace RoutePlanningAgent.Tests.AI;

public class RouteAiServiceParsingTests
{
    private static readonly Guid RouteId = Guid.NewGuid();

    [Fact]
    public void ParseLlmResponse_JsonThuan()
    {
        const string json = """{"summary": "Tuyến ổn", "confidence": 0.87, "suggestions": ["Đi sớm hơn", "Tránh giờ cao điểm"]}""";

        var dto = RouteAiService.ParseLlmResponse(json, RouteId, null);

        Assert.Equal("Tuyến ổn", dto.Summary);
        Assert.Equal(0.87, dto.ConfidenceScore);
        Assert.Equal(2, dto.Suggestions.Count);
    }

    [Fact]
    public void ParseLlmResponse_JsonTrongCodeFence()
    {
        const string fenced = """
            ```json
            {"summary": "Fence OK", "confidence": 0.5, "suggestions": []}
            ```
            """;

        var dto = RouteAiService.ParseLlmResponse(fenced, RouteId, null);

        Assert.Equal("Fence OK", dto.Summary);
    }

    [Fact]
    public void ParseLlmResponse_TextTuDo_DungNguyenVanLamSummary()
    {
        const string text = "Đây là text tự do, không phải JSON.";

        var dto = RouteAiService.ParseLlmResponse(text, RouteId, null);

        Assert.Equal(text, dto.Summary);
    }
}

