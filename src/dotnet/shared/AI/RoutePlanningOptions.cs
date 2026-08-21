namespace Shared.AI;

public class RoutePlanningOptions
{
    public string DefaultProvider { get; set; } = "Gemini";

    /// <summary>Model id dùng cho Gemini provider (gói Standard).</summary>
    public string GeminiModelId { get; set; } = "gemini-2.5-flash";

    public ApiKeyPoolOptions KeyPool { get; set; } = new();
}
