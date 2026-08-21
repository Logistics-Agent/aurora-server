using System.Text.Json;
using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;

namespace DocumentOcr.Application.Jobs;

public sealed record NormalizedOcrResult(
    string Json,
    string FieldConfidenceJson,
    decimal Confidence,
    bool NeedsReview);

public static class DocumentOcrResultNormalizer
{
    public static NormalizedOcrResult Normalize(
        OcrProviderResult result,
        decimal reviewConfidenceThreshold)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (reviewConfidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(reviewConfidenceThreshold));

        var orderedFields = result.Fields.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        var confidence = Math.Round(
            orderedFields.Average(field => field.Confidence),
            4,
            MidpointRounding.AwayFromZero);
        var fields = orderedFields.ToDictionary(
            field => field.Name,
            field => new { value = field.Value, confidence = field.Confidence },
            StringComparer.Ordinal);
        var fieldConfidence = orderedFields.ToDictionary(
            field => field.Name,
            field => field.Confidence,
            StringComparer.Ordinal);
        var normalized = new
        {
            schemaVersion = 1,
            documentType = result.DetectedDocumentType.ToString(),
            fields,
            references = new { text = result.TextReference, layout = result.LayoutReference }
        };
        var needsReview = confidence < reviewConfidenceThreshold ||
            RequiredFields(result.DetectedDocumentType).Any(required =>
                !fields.ContainsKey(required));

        return new NormalizedOcrResult(
            JsonSerializer.Serialize(normalized),
            JsonSerializer.Serialize(fieldConfidence),
            confidence,
            needsReview);
    }

    private static string[] RequiredFields(OcrDocumentType documentType) => documentType switch
    {
        OcrDocumentType.CommercialInvoice => ["documentNumber", "documentDate"],
        OcrDocumentType.PackingList or OcrDocumentType.BillOfLading => ["documentNumber"],
        _ => []
    };
}
