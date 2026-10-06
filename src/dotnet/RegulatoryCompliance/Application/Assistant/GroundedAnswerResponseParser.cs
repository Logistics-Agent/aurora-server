using System.Text.Json;
using System.Text.RegularExpressions;

namespace RegulatoryCompliance.Application.Assistant;

public static class GroundedAnswerResponseParser
{
    private static readonly string[] RequiredProperties =
    [
        "answer",
        "citations",
        "knowledgeReferences",
        "conflicts",
        "insufficientEvidence",
        "missingInformation"
    ];

    public static LlmParsedResponse Parse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return Invalid("LLM returned empty content.");
        }

        var clean = StripCodeFence(rawContent.Trim());

        try
        {
            using var document = JsonDocument.Parse(clean);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                RequiredProperties.Any(property => !HasPropertyIgnoreCase(document.RootElement, property)))
            {
                return Invalid("The AI response was missing one or more required JSON fields.");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var parsed = JsonSerializer.Deserialize<LlmParsedResponse>(clean, options);
            return parsed ?? Invalid("The AI response could not be deserialized.");
        }
        catch (JsonException)
        {
            return Invalid("The AI response was not valid structured output.");
        }
    }

    private static string StripCodeFence(string content)
    {
        if (!content.StartsWith("```", StringComparison.Ordinal))
        {
            return content;
        }

        var match = Regex.Match(content, @"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : content;
    }

    private static bool HasPropertyIgnoreCase(JsonElement root, string propertyName) =>
        root.EnumerateObject().Any(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));

    private static LlmParsedResponse Invalid(string reason) =>
        new(string.Empty, [], [], [], true, [reason])
        {
            IsStructuredOutputValid = false
        };
}
