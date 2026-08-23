using System.Text.Json;
using AiGovernance.Grpc;
using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;
using Grpc.Core;
using Shared.Security;

namespace DocumentOcr.Infrastructure.Providers;

public sealed class AiGovernanceOcrProvider(
    AiExecutionService.AiExecutionServiceClient aiExecutionClient,
    ICurrentUserService currentUser) : IOcrProvider
{
    public string Name => "AiGovernance-Ocr";

    public async Task<OcrProviderResult> ExtractAsync(
        OcrProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = OcrPromptBuilder.BuildPrompt(request.ExtractionMode, request.DocumentTypeHint, request.FileName);

        var generateRequest = new AiGenerateRequest
        {
            CapabilityCode = "ocr.extract",
            Prompt = prompt,
            MaxOutputTokens = 8192,
            EstimatedInputTokens = Math.Max(200, (int)(request.Content.Bytes.Length / 100))
        };

        // Add multimodal file reference
        if (!string.IsNullOrWhiteSpace(request.StorageReference))
        {
            generateRequest.InputParts.Add(new AiInputPart
            {
                File = new AiFileReference
                {
                    StorageReference = request.StorageReference,
                    MimeType = request.MimeType,
                    FileName = request.FileName
                }
            });
        }

        var headers = new Metadata
        {
            { "x-service-id", "document-ocr" }
        };

        if (currentUser.TenantId.HasValue)
            headers.Add("x-tenant-id", currentUser.TenantId.Value.ToString());
        else if (request.TenantId != Guid.Empty)
            headers.Add("x-tenant-id", request.TenantId.ToString());

        if (currentUser.UserId.HasValue)
            headers.Add("x-user-id", currentUser.UserId.Value.ToString());

        if (!string.IsNullOrEmpty(currentUser.TraceId))
            headers.Add("x-trace-id", currentUser.TraceId);

        try
        {
            var response = await aiExecutionClient.GenerateAsync(
                generateRequest,
                headers,
                deadline: DateTime.UtcNow.AddSeconds(60),
                cancellationToken: cancellationToken);

            var content = response.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new OcrProviderException(
                    OcrProviderFailureKind.InvalidDocument,
                    "EMPTY_OCR_RESPONSE",
                    "AiGovernance returned an empty OCR response.");
            }

            if (request.ExtractionMode == OcrExtractionMode.FullText)
            {
                return OcrProviderResult.Create(
                    request.DocumentTypeHint != OcrDocumentType.Unspecified ? request.DocumentTypeHint : OcrDocumentType.Other,
                    [OcrExtractedField.Create("full_text_length", content.Length.ToString(), 0.99m)],
                    response.DecisionId ?? Guid.NewGuid().ToString(),
                    null,
                    null,
                    $"Provider: {response.Provider}, Model: {response.Model}",
                    content,
                    OcrExtractionMode.FullText);
            }

            // Structured or Both
            var jsonText = ExtractJsonBlock(content);
            return ParseStructuredResponse(jsonText, request, response.DecisionId ?? Guid.NewGuid().ToString(), response.Provider, response.Model);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new OcrProviderException(
                OcrProviderFailureKind.Transient,
                "AIGOVERNANCE_UNAVAILABLE",
                $"AiGovernance service unavailable or timed out: {ex.Status.Detail}");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
        {
            throw new OcrProviderException(
                OcrProviderFailureKind.Permanent,
                "GOVERNANCE_DENIED",
                $"AiGovernance policy denied OCR extraction: {ex.Status.Detail}");
        }
        catch (RpcException ex)
        {
            throw new OcrProviderException(
                OcrProviderFailureKind.Permanent,
                $"AIGOVERNANCE_ERROR_{ex.StatusCode}",
                $"AiGovernance execution failed: {ex.Status.Detail}");
        }
    }

    private static OcrProviderResult ParseStructuredResponse(
        string jsonText,
        OcrProviderRequest request,
        string decisionId,
        string provider,
        string model)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var detectedType = request.DocumentTypeHint != OcrDocumentType.Unspecified
                ? request.DocumentTypeHint
                : OcrDocumentType.CommercialInvoice;

            if (root.TryGetProperty("detected_type", out var typeProp) &&
                Enum.TryParse<OcrDocumentType>(typeProp.GetString(), true, out var parsedType) &&
                parsedType != OcrDocumentType.Unspecified)
            {
                detectedType = parsedType;
            }

            var fields = new List<OcrExtractedField>();
            if (root.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var fieldElem in fieldsProp.EnumerateArray())
                {
                    var name = fieldElem.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var val = fieldElem.TryGetProperty("value", out var v) ? v.GetString() : null;
                    var conf = fieldElem.TryGetProperty("confidence", out var c) && c.TryGetDecimal(out var cd) ? cd : 0.90m;

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(val))
                    {
                        fields.Add(OcrExtractedField.Create(name, val, Math.Clamp(conf, 0.0m, 1.0m)));
                    }
                }
            }

            if (fields.Count == 0)
            {
                // Fallback default field from structured json
                fields.Add(OcrExtractedField.Create("raw_structured_json", jsonText.Length > 3000 ? jsonText[..3000] : jsonText, 0.85m));
            }

            return OcrProviderResult.Create(
                detectedType,
                fields,
                decisionId,
                null,
                null,
                $"Provider: {provider}, Model: {model}",
                null,
                OcrExtractionMode.Structured);
        }
        catch (JsonException)
        {
            // Fallback when JSON parsing fails
            return OcrProviderResult.Create(
                request.DocumentTypeHint != OcrDocumentType.Unspecified ? request.DocumentTypeHint : OcrDocumentType.Other,
                [OcrExtractedField.Create("unparsed_content", jsonText.Length > 2000 ? jsonText[..2000] : jsonText, 0.50m)],
                decisionId,
                null,
                null,
                $"Provider: {provider}, Model: {model}, MalformedJSONFallback",
                null,
                OcrExtractionMode.Structured);
        }
    }

    private static string ExtractJsonBlock(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endIdx = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIdx > 7)
            {
                return trimmed[7..endIdx].Trim();
            }
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            var endIdx = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIdx > 3)
            {
                return trimmed[3..endIdx].Trim();
            }
        }
        return trimmed;
    }
}
