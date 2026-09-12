using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentOcr.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOcrEventContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InitiatingCorrelationId",
                table: "document_ocr_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "document_ocr_jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE document_ocr_jobs SET \"Purpose\" = 'ShipmentDocument' WHERE \"Purpose\" IS NULL;");
            migrationBuilder.Sql(
                "UPDATE document_ocr_jobs " +
                "SET \"InitiatingCorrelationId\" = (" +
                "substr(md5(\"Id\"::text), 1, 8) || '-' || " +
                "substr(md5(\"Id\"::text), 9, 4) || '-' || " +
                "substr(md5(\"Id\"::text), 13, 4) || '-' || " +
                "substr(md5(\"Id\"::text), 17, 4) || '-' || " +
                "substr(md5(\"Id\"::text), 21, 12))::uuid " +
                "WHERE \"InitiatingCorrelationId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "InitiatingCorrelationId",
                table: "document_ocr_jobs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "document_ocr_jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitiatingCorrelationId",
                table: "document_ocr_jobs");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "document_ocr_jobs");
        }
    }
}
