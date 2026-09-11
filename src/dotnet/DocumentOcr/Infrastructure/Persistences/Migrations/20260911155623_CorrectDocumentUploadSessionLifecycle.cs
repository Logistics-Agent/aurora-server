using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentOcr.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class CorrectDocumentUploadSessionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredHeadersJson",
                table: "document_upload_sessions");

            migrationBuilder.DropColumn(
                name: "WriteUrl",
                table: "document_upload_sessions");

            migrationBuilder.AddColumn<string>(
                name: "CleanupStatus",
                table: "document_upload_sessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "StateVersion",
                table: "document_upload_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanupStatus",
                table: "document_upload_sessions");

            migrationBuilder.DropColumn(
                name: "StateVersion",
                table: "document_upload_sessions");

            migrationBuilder.AddColumn<string>(
                name: "RequiredHeadersJson",
                table: "document_upload_sessions",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WriteUrl",
                table: "document_upload_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }
    }
}
