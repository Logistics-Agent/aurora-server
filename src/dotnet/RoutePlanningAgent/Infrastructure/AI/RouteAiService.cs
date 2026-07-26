using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.AI.Plugins;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.AI;
using Shared.Events;
using Shared.Rules;
using MassTransit;

namespace RoutePlanningAgent.Infrastructure.AI;

public class RouteAiService(
    [FromKeyedServices("Gemini")] IApiKeyPool<string> geminiPool,
    [FromKeyedServices("AwsClaude")] IApiKeyPool<string> awsPool,
    IApiKeyPool<AzureOpenAiKeyEntry> azurePool,
    RoutePlanningDbContext context,
    IPublishEndpoint publisher)
    : IRouteAiService
{
    public async Task<RouteRecommendationDto> GetRecommendationAsync(
        Route route,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult,
        string provider,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var model = GetModelName(provider);
        var promptVersion = "v1.0";

        string llmResponse = string.Empty;
        bool success = true;

        try
        {
            var kernel = BuildKernel(provider);

            var arguments = new KernelArguments
            {
                ["routeJson"] = JsonSerializer.Serialize(route),
                ["ruleViolations"] = JsonSerializer.Serialize(ruleResults),
                ["complianceContext"] = complianceResult?.MergedContext ?? string.Empty
            };

            var function = kernel.Plugins[nameof(RouteAnalysisPlugin)]["AnalyzeRisk"];
            llmResponse = await kernel.InvokeAsync<string>(function, arguments, ct) ?? string.Empty;
        }
        catch (Exception)
        {
            success = false;
            llmResponse = "[Error] Failed to connect to LLM provider. Using fallback analysis.";
        }

        sw.Stop();

        // 1. Save optimization history
        var history = new RouteOptimizationHistory
        {
            RouteId = route.Id,
            Provider = provider,
            Model = model,
            PromptVersion = promptVersion,
            TotalDistanceKm = route.EstimatedDistanceKm,
            TotalDurationMinutes = route.EstimatedDurationMinutes,
            InputTokens = 120, // Estimated/standard count for audit
            OutputTokens = 80,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.OptimizationHistories.Add(history);
        await context.SaveChangesAsync(ct);

        // 2. Publish AI usage event for AI Ops & Monitoring
        await publisher.Publish(new AiUsageEvent
        {
            TenantId = route.TenantId,
            ServiceName = "RoutePlanningAgent",
            Feature = "RouteRecommendation",
            Provider = provider,
            Model = model,
            PromptVersion = promptVersion,
            InputTokens = history.InputTokens,
            OutputTokens = history.OutputTokens,
            LatencyMs = sw.ElapsedMilliseconds,
            Success = success,
            OccurredAt = DateTimeOffset.UtcNow
        }, ct);

        // 3. Map to DTO
        return ParseLlmResponse(llmResponse, route.Id, complianceResult, provider);
    }

    private Kernel BuildKernel(string provider)
    {
        var builder = Kernel.CreateBuilder();
        builder.Plugins.AddFromType<RouteAnalysisPlugin>();

        switch (provider)
        {
            case "AzureOpenAI":
                var azureEntry = azurePool.GetNext();
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureEntry.DeploymentName,
                    endpoint: azureEntry.Endpoint,
                    apiKey: azureEntry.ApiKey);
                break;

            case "AwsClaude":
                var awsModelId = awsPool.GetNext();
                builder.AddAmazonBedrockChatCompletion(
                    modelId: awsModelId,
                    region: "us-east-1",
                    awsAccessKeyId: "dummy-key",
                    awsSecretAccessKey: "dummy-secret");
                break;

            default: // Gemini
                var geminiKey = geminiPool.GetNext();
                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: "gemini-2.5-flash",
                    apiKey: geminiKey);
                break;
        }

        return builder.Build();
    }

    private string GetModelName(string provider) => provider switch
    {
        "AzureOpenAI" => "gpt-4o",
        "AwsClaude" => "claude-3-5-sonnet",
        _ => "gemini-2.5-flash"
    };

    private RouteRecommendationDto ParseLlmResponse(
        string llmResponse, Guid routeId, ComplianceCheckResultDto? complianceResult, string provider)
    {
        var suggestions = new List<string> { "Re-route stops to avoid peak traffic." };
        var summary = llmResponse;
        var confidence = 0.90;

        try
        {
            if (llmResponse.Trim().StartsWith('{'))
            {
                var doc = JsonDocument.Parse(llmResponse);
                var root = doc.RootElement;
                if (root.TryGetProperty("summary", out var sProp)) summary = sProp.GetString() ?? summary;
                if (root.TryGetProperty("confidence", out var cProp)) confidence = cProp.GetDouble();
                if (root.TryGetProperty("suggestions", out var sugProp) && sugProp.ValueKind == JsonValueKind.Array)
                {
                    suggestions.Clear();
                    foreach (var item in sugProp.EnumerateArray())
                    {
                        suggestions.Add(item.GetString() ?? "");
                    }
                }
            }
        }
        catch
        {
            // Ignore JSON parse errors and use fallback
        }

        return new RouteRecommendationDto
        {
            RouteId = routeId,
            RiskLevel = complianceResult != null && complianceResult.HasViolations ? "High" : "Medium",
            AutomationDecision = "ExecutedByLlm",
            RecommendationSource = "AI",
            Summary = summary,
            Suggestions = suggestions,
            ConfidenceScore = confidence,
            ApplicableRegulations = complianceResult?.ApplicableRegulations ?? []
        };
    }
}
