using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ShipmentWorkflow.Application.Commands.Shipments;

internal static class DocumentIntakePersistenceErrors
{
    internal const string IntakeIdempotencyKeyConstraint =
        "IX_document_intakes_TenantId_ShipmentId_IdempotencyKey";
    internal const string IntakeDocumentIdConstraint =
        "IX_document_intakes_TenantId_ShipmentId_DocumentId";
    internal const string AttachmentIdempotencyKeyConstraint =
        "IX_shipment_documents_TenantId_ShipmentId_IdempotencyKey";
    internal const string AttachmentStorageReferenceConstraint =
        "IX_shipment_documents_TenantId_ShipmentId_StorageReference";

    internal static string? GetUniqueConstraintName(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException
            ? postgresException.ConstraintName
            : null;
    }

}
