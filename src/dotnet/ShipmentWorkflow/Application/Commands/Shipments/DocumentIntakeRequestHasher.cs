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

    internal static string ForAttachment(AttachShipmentDocumentCommand request, string storageReference)
    {
        return Hash(new
        {
            request.ShipmentId,
            request.FileName,
            request.DocumentType,
            storageReference,
            request.OCRStatus,
            request.OCRConfidence,
            request.ExtractedDataJson,
            request.IdempotencyKey,
            request.UploadId
        });
    }

    private static string Hash(object value)
    {
        var canonicalJson = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)))
            .ToLowerInvariant();
    }
}
