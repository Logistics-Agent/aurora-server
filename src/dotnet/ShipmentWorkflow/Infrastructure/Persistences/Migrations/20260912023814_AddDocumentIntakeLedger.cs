using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipmentWorkflow.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIntakeLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageReference",
                table: "shipment_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "shipment_documents",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadId",
                table: "shipment_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "shipment_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_intakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StateVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_intakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_intakes_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_intakes_ShipmentId",
                table: "document_intakes",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_intakes_TenantId_ShipmentId_DocumentId",
                table: "document_intakes",
                columns: new[] { "TenantId", "ShipmentId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_intakes_TenantId_ShipmentId_IdempotencyKey",
                table: "document_intakes",
                columns: new[] { "TenantId", "ShipmentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_TenantId_ShipmentId_IdempotencyKey",
                table: "shipment_documents",
                columns: new[] { "TenantId", "ShipmentId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_TenantId_ShipmentId_StorageReference",
                table: "shipment_documents",
                columns: new[] { "TenantId", "ShipmentId", "StorageReference" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL AND \"StorageReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shipment_documents_TenantId_ShipmentId_IdempotencyKey",
                table: "shipment_documents");

            migrationBuilder.DropIndex(
                name: "IX_shipment_documents_TenantId_ShipmentId_StorageReference",
                table: "shipment_documents");

            migrationBuilder.DropTable(
                name: "document_intakes");

            migrationBuilder.DropColumn(
                name: "StorageReference",
                table: "shipment_documents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "shipment_documents");

            migrationBuilder.DropColumn(
                name: "UploadId",
                table: "shipment_documents");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "shipment_documents");
        }
    }
}
