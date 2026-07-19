namespace Shared.AI;

public class RoutePlanningOptions
{
    public string DefaultProvider { get; set; } = "Gemini";
    public ApiKeyPoolOptions KeyPool { get; set; } = new();
}
