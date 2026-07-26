using System.Collections.Generic;

namespace Shared.AI;

public class ApiKeyPoolOptions
{
    public List<string> GeminiApiKeys { get; set; } = [];
    public List<AzureOpenAiKeyEntry> AzureOpenAiKeys { get; set; } = [];
    public List<string> AwsClaudeModelIds { get; set; } = [];
}
