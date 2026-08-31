using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class NotificationFcmAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_processed_notification_events_EventId_Rule_UserId",
                table: "processed_notification_events");

            migrationBuilder.DropIndex(
                name: "IX_notification_devices_TenantId_UserId_FcmToken",
                table: "notification_devices");

            // Collapse legacy per-user receipts before enforcing one receipt per event/rule.
            migrationBuilder.Sql("""
                DELETE FROM processed_notification_events AS duplicate
                USING processed_notification_events AS retained
                WHERE duplicate."TenantId" = retained."TenantId"
                  AND duplicate."EventId" = retained."EventId"
                  AND duplicate."Rule" = retained."Rule"
                  AND duplicate."Id" > retained."Id";
                """);

            // Keep the newest active owner when legacy data contains a duplicated FCM token.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "FcmToken" ORDER BY "LastSeenAt" DESC, "Id" DESC) AS row_number
                    FROM notification_devices
                    WHERE "IsActive" = TRUE
                )
                UPDATE notification_devices AS device
                SET "IsActive" = FALSE
                FROM ranked
                WHERE device."Id" = ranked."Id" AND ranked.row_number > 1;
                """);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "processed_notification_events");

            migrationBuilder.AddColumn<int>(
                name: "Outcome",
                table: "processed_notification_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecipientCount",
                table: "processed_notification_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "notification_delivery_attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_processed_notification_events_TenantId_EventId_Rule",
                table: "processed_notification_events",
                columns: new[] { "TenantId", "EventId", "Rule" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_devices_FcmToken",
                table: "notification_devices",
                column: "FcmToken",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_notification_delivery_attempts_Status_NextAttemptAt",
                table: "notification_delivery_attempts",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_processed_notification_events_TenantId_EventId_Rule",
                table: "processed_notification_events");

            migrationBuilder.DropIndex(
                name: "IX_notification_devices_FcmToken",
                table: "notification_devices");

            migrationBuilder.DropIndex(
                name: "IX_notification_delivery_attempts_Status_NextAttemptAt",
                table: "notification_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "processed_notification_events");

            migrationBuilder.DropColumn(
                name: "RecipientCount",
                table: "processed_notification_events");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "notification_delivery_attempts");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "processed_notification_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_processed_notification_events_EventId_Rule_UserId",
                table: "processed_notification_events",
                columns: new[] { "EventId", "Rule", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_devices_TenantId_UserId_FcmToken",
                table: "notification_devices",
                columns: new[] { "TenantId", "UserId", "FcmToken" },
                unique: true);
        }
    }
}
