using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentOcr.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_upload_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeclaredMimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DeclaredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DeclaredContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WriteUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequiredHeadersJson = table.Column<string>(type: "jsonb", nullable: false),
                    MaximumSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VerifiedMimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    VerifiedSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    VerifiedContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_upload_sessions", x => x.Id);
                    table.UniqueConstraint("AK_document_upload_sessions_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_upload_sessions_TenantId_IdempotencyKey",
                table: "document_upload_sessions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_upload_sessions_TenantId_ObjectKey",
                table: "document_upload_sessions",
                columns: new[] { "TenantId", "ObjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_upload_sessions_TenantId_Status_ExpiresAt",
                table: "document_upload_sessions",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_upload_sessions");
        }
    }
}
