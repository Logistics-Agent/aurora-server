using DocumentOcr.Grpc;
using Grpc.Core;
using ShipmentWorkflow.Grpc;
using Shared.Security;

namespace StaffBff.Services;

public sealed record CargoSnapshotItem(
    string Name,
    string? HsCode,
    int Quantity,
    string Unit,
    double WeightKg,
    double VolumeM3,
    bool IsDangerousGoods,
    string? DangerousGoodsCode,
    string? PackageType);

public sealed record ReviewedDocumentSnapshot(
    Guid ExternalDocumentId,
    string DocumentType,
    string NormalizedJson,
    double ExtractionConfidence,
    bool NeedsReview);

public sealed record CanonicalComplianceSnapshot(
    Guid ShipmentId,
    string ShipmentVersion,
    IReadOnlyList<CargoSnapshotItem> Cargo,
    string OriginCountryCode,
    string DestinationCountryCode,
    string TransportMode,
    IReadOnlyList<string> JurisdictionCodes,
    IReadOnlyList<ReviewedDocumentSnapshot> Documents,
    IReadOnlyList<string> MissingEvidence,
    DateTimeOffset EffectiveAt);

public sealed class ComplianceSnapshotIncompleteException(IReadOnlyCollection<string> missingFields)
    : Exception("The shipment snapshot is incomplete.")
{
    public IReadOnlyCollection<string> MissingFields { get; } = missingFields;
}

public sealed class ComplianceSnapshotComposer(
    ShipmentWorkflowService.ShipmentWorkflowServiceClient shipmentClient,
    DocumentOcrService.DocumentOcrServiceClient documentOcrClient,
    ICurrentUserService currentUser)
{
    private const int DocumentPageSize = 100;

    public async Task<CanonicalComplianceSnapshot> ComposeAsync(
        string shipmentId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken = default)
    {
        var parsedShipmentId = ParseId(shipmentId, "shipmentId");
        RequireTenant();
        if (effectiveAt == default)
            throw new ArgumentException("effectiveAt is required.", nameof(effectiveAt));

        var shipment = await shipmentClient.GetShipmentAsync(
            new GetShipmentRequest { Id = parsedShipmentId.ToString() },
            cancellationToken: cancellationToken);

        ValidateShipmentIdentity(shipment, parsedShipmentId);
        var missingFields = new List<string>();
        var version = shipment.UpdatedAt?.ToDateTimeOffset();
        if (!version.HasValue)
            missingFields.Add("shipmentVersion");

        var origin = NormalizeRequired(shipment.OriginCountry, "originCountry", missingFields);
        var destination = NormalizeRequired(shipment.DestinationCountry, "destinationCountry", missingFields);
        var transportMode = NormalizeRequired(shipment.TransportMode, "transportMode", missingFields);
        var cargo = ComposeCargo(shipment.CargoItems, missingFields);
        if (cargo.Count == 0)
            missingFields.Add("cargo");

        if (missingFields.Count > 0)
            throw new ComplianceSnapshotIncompleteException(missingFields);

        var documents = await ComposeDocumentsAsync(
            parsedShipmentId,
            cancellationToken);

        return new CanonicalComplianceSnapshot(
            parsedShipmentId,
            version!.Value.ToString("O"),
            cargo,
            origin!,
            destination!,
            transportMode!,
            [origin!, destination!],
            documents.Documents,
            documents.MissingEvidence,
            effectiveAt);
    }

    private async Task<(IReadOnlyList<ReviewedDocumentSnapshot> Documents, IReadOnlyList<string> MissingEvidence)> ComposeDocumentsAsync(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var response = await documentOcrClient.ListDocumentJobsAsync(
            new ListDocumentJobsRequest
            {
                Page = 1,
                PageSize = DocumentPageSize,
                ExternalShipmentId = shipmentId.ToString()
            },
            cancellationToken: cancellationToken);

        var documents = new List<ReviewedDocumentSnapshot>();
        var missingEvidence = new List<string>();
        foreach (var job in response.Jobs)
        {
            var sourceId = !string.IsNullOrWhiteSpace(job.ExternalDocumentId)
                ? job.ExternalDocumentId
                : job.JobId;
            var label = string.IsNullOrWhiteSpace(sourceId) ? "unknown" : sourceId;

            if (job.Status != DocumentOcrJobStatus.Completed || job.NeedsReview)
            {
                var reason = job.NeedsReview || job.Status == DocumentOcrJobStatus.RequiresReview
                    ? "requires_review"
                    : "not_ready";
                missingEvidence.Add($"document:{label}:{reason}");
                continue;
            }

            if (!Guid.TryParse(job.ExternalDocumentId, out var documentId) || documentId == Guid.Empty)
                throw new ArgumentException(
                    $"Completed OCR document {label} has an invalid external document ID.",
                    nameof(job.ExternalDocumentId));
            if (string.IsNullOrWhiteSpace(job.NormalizedJson))
            {
                missingEvidence.Add($"document:{documentId}:normalized_data_missing");
                continue;
            }

            var documentType = job.DetectedDocumentType != OcrDocumentType.Unspecified
                ? job.DetectedDocumentType
                : job.DocumentTypeHint;
            if (documentType == OcrDocumentType.Unspecified)
            {
                missingEvidence.Add($"document:{documentId}:type_missing");
                continue;
            }

            documents.Add(new ReviewedDocumentSnapshot(
                documentId,
                documentType.ToString(),
                job.NormalizedJson,
                Math.Clamp(job.Confidence, 0d, 1d),
                false));
        }

        return (documents, missingEvidence);
    }

    private static IReadOnlyList<CargoSnapshotItem> ComposeCargo(
        IEnumerable<CargoItemResponse> source,
        ICollection<string> missingFields)
    {
        var cargo = new List<CargoSnapshotItem>();
        var index = 0;
        foreach (var item in source)
        {
            var prefix = $"cargo[{index}]";
            if (string.IsNullOrWhiteSpace(item.Name))
                missingFields.Add($"{prefix}.name");
            if (item.Quantity <= 0)
                missingFields.Add($"{prefix}.quantity");
            if (item.WeightKg <= 0)
                missingFields.Add($"{prefix}.weightKg");
            if (item.VolumeM3 < 0)
                missingFields.Add($"{prefix}.volumeM3");
            if (string.IsNullOrWhiteSpace(item.Unit))
                missingFields.Add($"{prefix}.unit");
            if (item.IsDangerousGoods)
                missingFields.Add($"{prefix}.dangerousGoodsCode");

            cargo.Add(new CargoSnapshotItem(
                item.Name.Trim(),
                NullIfWhiteSpace(item.HsCode),
                item.Quantity,
                item.Unit.Trim(),
                item.WeightKg,
                item.VolumeM3,
                item.IsDangerousGoods,
                null,
                NullIfWhiteSpace(item.PackageType)));
            index++;
        }

        return cargo;
    }

    private void ValidateShipmentIdentity(ShipmentResponse shipment, Guid expectedShipmentId)
    {
        if (!Guid.TryParse(shipment.Id, out var actualShipmentId) || actualShipmentId != expectedShipmentId)
            throw new KeyNotFoundException("Shipment was not found.");
        if (!Guid.TryParse(shipment.TenantId, out var shipmentTenantId) ||
            shipmentTenantId != RequireTenant())
            throw new KeyNotFoundException("Shipment was not found.");
    }

    private Guid RequireTenant() =>
        currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new InvalidOperationException("Tenant context is required.");

    private static Guid ParseId(string value, string fieldName) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new ArgumentException($"{fieldName} is invalid.", fieldName);

    private static string? NormalizeRequired(
        string value,
        string fieldName,
        ICollection<string> missingFields)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missingFields.Add(fieldName);
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
