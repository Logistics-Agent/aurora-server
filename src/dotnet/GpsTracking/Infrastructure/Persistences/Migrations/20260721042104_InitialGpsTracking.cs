using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GpsTracking.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class InitialGpsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumed_integration_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumed_integration_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "geofences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    RadiusMeters = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geofences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gps_positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalReadingId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    SpeedKph = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    HeadingDegrees = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    AccuracyMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_tracking_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    LastEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_tracking_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_shipment_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_shipment_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "geofence_presences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeofenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsInside = table.Column<bool>(type: "boolean", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geofence_presences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_geofence_presences_geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalTable: "geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "current_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    SpeedKph = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    HeadingDegrees = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    AccuracyMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StationarySince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_current_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_current_locations_gps_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "gps_positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeofenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monitoring_alerts_geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalTable: "geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_alerts_gps_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "gps_positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consumed_integration_events_SourceEventType_SourceEventId",
                table: "consumed_integration_events",
                columns: new[] { "SourceEventType", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consumed_integration_events_TenantId_ReceivedAt",
                table: "consumed_integration_events",
                columns: new[] { "TenantId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_current_locations_PositionId",
                table: "current_locations",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_current_locations_TenantId_RecordedAt",
                table: "current_locations",
                columns: new[] { "TenantId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_current_locations_TenantId_ShipmentId",
                table: "current_locations",
                columns: new[] { "TenantId", "ShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_current_locations_TenantId_StationarySince",
                table: "current_locations",
                columns: new[] { "TenantId", "StationarySince" });

            migrationBuilder.CreateIndex(
                name: "IX_current_locations_TenantId_VehicleId",
                table: "current_locations",
                columns: new[] { "TenantId", "VehicleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_geofence_presences_GeofenceId",
                table: "geofence_presences",
                column: "GeofenceId");

            migrationBuilder.CreateIndex(
                name: "IX_geofence_presences_TenantId_GeofenceId_VehicleId",
                table: "geofence_presences",
                columns: new[] { "TenantId", "GeofenceId", "VehicleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_geofences_TenantId_IsActive",
                table: "geofences",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_geofences_TenantId_ShipmentId_IsActive",
                table: "geofences",
                columns: new[] { "TenantId", "ShipmentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_geofences_TenantId_VehicleId_IsActive",
                table: "geofences",
                columns: new[] { "TenantId", "VehicleId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_gps_positions_TenantId_DeviceId_ExternalReadingId",
                table: "gps_positions",
                columns: new[] { "TenantId", "DeviceId", "ExternalReadingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gps_positions_TenantId_ShipmentId_RecordedAt_Id",
                table: "gps_positions",
                columns: new[] { "TenantId", "ShipmentId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_gps_positions_TenantId_VehicleId_RecordedAt_Id",
                table: "gps_positions",
                columns: new[] { "TenantId", "VehicleId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_alerts_GeofenceId",
                table: "monitoring_alerts",
                column: "GeofenceId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_alerts_PositionId",
                table: "monitoring_alerts",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_alerts_TenantId_DeduplicationKey",
                table: "monitoring_alerts",
                columns: new[] { "TenantId", "DeduplicationKey" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_alerts_TenantId_Status_OccurredAt",
                table: "monitoring_alerts",
                columns: new[] { "TenantId", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_alerts_TenantId_VehicleId_OccurredAt",
                table: "monitoring_alerts",
                columns: new[] { "TenantId", "VehicleId", "OccurredAt" });

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

            migrationBuilder.CreateIndex(
                name: "IX_shipment_tracking_states_TenantId_IsClosed_LastEventAt",
                table: "shipment_tracking_states",
                columns: new[] { "TenantId", "IsClosed", "LastEventAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_tracking_states_TenantId_ShipmentId",
                table: "shipment_tracking_states",
                columns: new[] { "TenantId", "ShipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_shipment_assignments_TenantId_ShipmentId",
                table: "vehicle_shipment_assignments",
                columns: new[] { "TenantId", "ShipmentId" },
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_shipment_assignments_TenantId_ShipmentId_EndedAt",
                table: "vehicle_shipment_assignments",
                columns: new[] { "TenantId", "ShipmentId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_shipment_assignments_TenantId_VehicleId",
                table: "vehicle_shipment_assignments",
                columns: new[] { "TenantId", "VehicleId" },
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_shipment_assignments_TenantId_VehicleId_EndedAt",
                table: "vehicle_shipment_assignments",
                columns: new[] { "TenantId", "VehicleId", "EndedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumed_integration_events");

            migrationBuilder.DropTable(
                name: "current_locations");

            migrationBuilder.DropTable(
                name: "geofence_presences");

            migrationBuilder.DropTable(
                name: "monitoring_alerts");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "shipment_tracking_states");

            migrationBuilder.DropTable(
                name: "vehicle_shipment_assignments");

            migrationBuilder.DropTable(
                name: "geofences");

            migrationBuilder.DropTable(
                name: "gps_positions");
        }
    }
}
