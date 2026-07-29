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

    [Fact]
    public void ExtractUsage_GeminiMetadata()
    {
        var metadata = new Dictionary<string, object?>
        {
            ["PromptTokenCount"] = 123,
            ["CandidatesTokenCount"] = 45
        };

        var (input, output) = RouteAiService.ExtractUsage(metadata);

        Assert.Equal(123, input);
        Assert.Equal(45, output);
    }

    [Fact]
    public void ExtractUsage_AzureUsageObject_QuaReflection()
    {
        var metadata = new Dictionary<string, object?>
        {
            ["Usage"] = new FakeUsage(200, 80)
        };

        var (input, output) = RouteAiService.ExtractUsage(metadata);

        Assert.Equal(200, input);
        Assert.Equal(80, output);
    }

    [Fact]
    public void ExtractUsage_KhongCoMetadata_TraVe0_KhongFabricate()
    {
        Assert.Equal((0, 0), RouteAiService.ExtractUsage(null));
        Assert.Equal((0, 0), RouteAiService.ExtractUsage(new Dictionary<string, object?>()));
    }

    private sealed record FakeUsage(int InputTokenCount, int OutputTokenCount);
}
