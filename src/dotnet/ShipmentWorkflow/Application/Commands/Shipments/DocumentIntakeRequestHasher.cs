using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.Commands.Shipments;

internal static class DocumentIntakeRequestHasher
{
    internal static string ForIntake(
        Guid shipmentId,
        Guid uploadId,
        string storageReference,
        string fileName,
        DocumentType documentType,
        string idempotencyKey)
    {
        return Hash(new
        {
            shipmentId,
            uploadId,
            storageReference = storageReference.Trim(),
            fileName = fileName.Trim(),
            documentType,
            idempotencyKey = idempotencyKey.Trim()
        });
    }

    internal static string ForAttachment(
        AttachShipmentDocumentCommand request,
        string storageReference,
        string effectiveStorageUrl)
    {
        return Hash(new
        {
            request.ShipmentId,
            fileName = request.FileName.Trim(),
            documentType = request.DocumentType.ToString(),
            storageUrl = effectiveStorageUrl.Trim(),
            storageReference = storageReference.Trim(),
            ocrStatus = request.OCRStatus.ToString(),
            ocrConfidence = request.OCRConfidence.HasValue
                ? (decimal?)decimal.Round(request.OCRConfidence.Value, 4, MidpointRounding.ToEven)
                : null,
            extractedDataJson = CanonicalizeJson(request.ExtractedDataJson),
            idempotencyKey = request.IdempotencyKey?.Trim(),
            request.UploadId
        });
    }

    private static string? CanonicalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(document.RootElement, writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(item, writer);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                if (element.TryGetDecimal(out var decimalValue))
                    writer.WriteNumberValue(decimalValue);
                else
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException("Unsupported JSON value kind.");
        }
    }

    private static string Hash(object value)
    {
        var canonicalJson = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)))
            .ToLowerInvariant();
    }
}
