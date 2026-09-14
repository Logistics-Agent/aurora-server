using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RegulatoryCompliance.Application.Evaluations;

public sealed record CanonicalComplianceSnapshot(string Json, string SnapshotHash);

public static class ComplianceSnapshotCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static CanonicalComplianceSnapshot Canonicalize(ComplianceEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var canonical = new CanonicalSnapshot(
            input.ExternalShipmentId,
            input.ShipmentVersion.Trim(),
            Normalize(input.OriginCountryCode),
            Normalize(input.DestinationCountryCode),
            Normalize(input.TransportMode),
            input.JurisdictionCodes
                .Select(Normalize)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            input.Cargo
                .Select(item => new CanonicalCargo(
                    Normalize(item.Name),
                    NormalizeOptional(item.HsCode),
                    item.Quantity,
                    Normalize(item.Unit),
                    item.WeightKg,
                    item.VolumeM3,
                    item.IsDangerousGoods,
                    NormalizeOptional(item.DangerousGoodsCode),
                    NormalizeOptional(item.PackageType)))
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .ThenBy(item => item.HsCode, StringComparer.Ordinal)
                .ThenBy(item => item.Quantity)
                .ToArray(),
            input.Documents
                .Select(item => new CanonicalDocument(
                    item.ExternalDocumentId,
                    Normalize(item.DocumentType),
                    CanonicalizeJson(item.NormalizedJson),
                    item.ExtractionConfidence,
                    item.NeedsReview))
                .OrderBy(item => item.ExternalDocumentId)
                .ToArray(),
            input.EffectiveAt.ToUniversalTime());

        var json = JsonSerializer.Serialize(canonical, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        return new CanonicalComplianceSnapshot(json, hash);
    }

    private static string CanonicalizeJson(string value)
    {
        var node = JsonNode.Parse(value) ?? throw new ArgumentException("JSON value is required.");
        return CanonicalizeNode(node).ToJsonString(JsonOptions);
    }

    private static JsonNode CanonicalizeNode(JsonNode node) =>
        node switch
        {
            JsonObject jsonObject => new JsonObject(
                jsonObject.OrderBy(property => property.Key, StringComparer.Ordinal)
                    .Select(property =>
                        new KeyValuePair<string, JsonNode?>(
                            property.Key,
                            property.Value is null ? null : CanonicalizeNode(property.Value)))
                    .ToArray()),
            JsonArray jsonArray => new JsonArray(
                jsonArray.Select(item => item is null ? null : CanonicalizeNode(item)).ToArray()),
            _ => node.DeepClone()
        };

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Normalize(value);

    private sealed record CanonicalSnapshot(
        Guid ShipmentId,
        string ShipmentVersion,
        string OriginCountryCode,
        string DestinationCountryCode,
        string TransportMode,
        IReadOnlyCollection<string> JurisdictionCodes,
        IReadOnlyCollection<CanonicalCargo> Cargo,
        IReadOnlyCollection<CanonicalDocument> Documents,
        DateTimeOffset EffectiveAt);

    private sealed record CanonicalCargo(
        string Name,
        string? HsCode,
        int Quantity,
        string Unit,
        decimal WeightKg,
        decimal VolumeM3,
        bool IsDangerousGoods,
        string? DangerousGoodsCode,
        string? PackageType);

    private sealed record CanonicalDocument(
        Guid ExternalDocumentId,
        string DocumentType,
        string NormalizedJson,
        decimal ExtractionConfidence,
        bool NeedsReview);
}
