namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ImportShipmentsResult(
    string? ImportRequestId,
    int TotalRows,
    int SuccessCount,
    int ErrorCount,
    IReadOnlyCollection<ImportShipmentRowResult> Rows);

public sealed record ImportShipmentRowResult(
    int RowNumber,
    bool Success,
    Guid? ShipmentId,
    string? ShipmentNo,
    string? Error);
