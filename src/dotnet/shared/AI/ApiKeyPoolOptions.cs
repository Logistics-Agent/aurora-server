using System.Collections.Generic;

namespace Shared.AI;

public class ApiKeyPoolOptions
{
    // Chỉ hỗ trợ 2 provider: Gemini (gói Standard) và AzureOpenAI (gói Enterprise)
    public List<string> GeminiApiKeys { get; set; } = [];
    public List<AzureOpenAiKeyEntry> AzureOpenAiKeys { get; set; } = [];
}
