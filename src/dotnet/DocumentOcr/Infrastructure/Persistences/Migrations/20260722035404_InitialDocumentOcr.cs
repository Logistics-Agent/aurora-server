using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentOcr.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class InitialDocumentOcr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_ocr_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StorageReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExternalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentTypeHint = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DetectedDocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NormalizedJson = table.Column<string>(type: "jsonb", nullable: true),
                    FieldConfidenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    NeedsReview = table.Column<bool>(type: "boolean", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcessingStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_ocr_jobs", x => x.Id);
                    table.UniqueConstraint("AK_document_ocr_jobs_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ocr_provider_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Diagnostics = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ocr_provider_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ocr_provider_attempts_document_ocr_jobs_TenantId_JobId",
                        columns: x => new { x.TenantId, x.JobId },
                        principalTable: "document_ocr_jobs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_ocr_jobs_Status_LeaseExpiresAt",
                table: "document_ocr_jobs",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_ocr_jobs_Status_NextAttemptAt_CreatedAt",
                table: "document_ocr_jobs",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_ocr_jobs_TenantId_ExternalDocumentId",
                table: "document_ocr_jobs",
                columns: new[] { "TenantId", "ExternalDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_ocr_jobs_TenantId_ExternalShipmentId",
                table: "document_ocr_jobs",
                columns: new[] { "TenantId", "ExternalShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_ocr_jobs_TenantId_IdempotencyKey",
                table: "document_ocr_jobs",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_ocr_jobs_TenantId_Status_CreatedAt_Id",
                table: "document_ocr_jobs",
                columns: new[] { "TenantId", "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_SourceEventType_SourceEventId",
                table: "inbox_messages",
                columns: new[] { "SourceEventType", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_TenantId_ReceivedAt",
                table: "inbox_messages",
                columns: new[] { "TenantId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ocr_provider_attempts_TenantId_JobId_StartedAt",
                table: "ocr_provider_attempts",
                columns: new[] { "TenantId", "JobId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ocr_provider_attempts_TenantId_ProviderRequestId",
                table: "ocr_provider_attempts",
                columns: new[] { "TenantId", "ProviderRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_EventId",
                table: "outbox_messages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt_RetryCount_OccurredAt",
                table: "outbox_messages",
                columns: new[] { "ProcessedAt", "RetryCount", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_TenantId_OccurredAt",
                table: "outbox_messages",
                columns: new[] { "TenantId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "ocr_provider_attempts");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "document_ocr_jobs");
        }
    }
}
