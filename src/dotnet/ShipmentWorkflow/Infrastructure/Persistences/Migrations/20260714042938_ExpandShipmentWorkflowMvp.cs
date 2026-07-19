using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipmentWorkflow.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class ExpandShipmentWorkflowMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualDeliveryTime",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualPickupTime",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EstimatedDeliveryTime",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EstimatedPickupTime",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "shipments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "shipments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "RouteId",
                table: "shipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportMode",
                table: "shipments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "VehicleId",
                table: "shipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "cargo_items",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeclaredValue",
                table: "cargo_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "cargo_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDangerousGoods",
                table: "cargo_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "cargo_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "cargo_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VolumeM3",
                table: "cargo_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shipment_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StorageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OCRStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OCRConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExtractedDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_documents_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_locations_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_milestones_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_TenantId_CustomerId",
                table: "shipments",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_TenantId_RouteId",
                table: "shipments",
                columns: new[] { "TenantId", "RouteId" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_TenantId_VehicleId",
                table: "shipments",
                columns: new[] { "TenantId", "VehicleId" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_ShipmentId",
                table: "shipment_documents",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_TenantId_OCRStatus",
                table: "shipment_documents",
                columns: new[] { "TenantId", "OCRStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_documents_TenantId_ShipmentId_DocumentType",
                table: "shipment_documents",
                columns: new[] { "TenantId", "ShipmentId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_locations_ShipmentId",
                table: "shipment_locations",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_locations_TenantId_ShipmentId_Sequence",
                table: "shipment_locations",
                columns: new[] { "TenantId", "ShipmentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_milestones_ShipmentId",
                table: "shipment_milestones",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_milestones_TenantId_RecordedAt",
                table: "shipment_milestones",
                columns: new[] { "TenantId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_milestones_TenantId_ShipmentId_RecordedAt",
                table: "shipment_milestones",
                columns: new[] { "TenantId", "ShipmentId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_documents");

            migrationBuilder.DropTable(
                name: "shipment_locations");

            migrationBuilder.DropTable(
                name: "shipment_milestones");

            migrationBuilder.DropIndex(
                name: "IX_shipments_TenantId_CustomerId",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_TenantId_RouteId",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_TenantId_VehicleId",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ActualDeliveryTime",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ActualPickupTime",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "EstimatedDeliveryTime",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "EstimatedPickupTime",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "TransportMode",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "DeclaredValue",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "IsDangerousGoods",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "cargo_items");

            migrationBuilder.DropColumn(
                name: "VolumeM3",
                table: "cargo_items");
        }
    }
}
