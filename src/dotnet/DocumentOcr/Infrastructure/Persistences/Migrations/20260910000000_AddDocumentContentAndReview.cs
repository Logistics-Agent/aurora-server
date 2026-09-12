using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentOcr.Infrastructure.Persistences.Migrations;

[DbContext(typeof(DocumentOcrDbContext))]
[Migration("20260910000000_AddDocumentContentAndReview")]
public sealed class AddDocumentContentAndReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExtractionMode",
            table: "document_ocr_jobs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
        migrationBuilder.Sql("UPDATE document_ocr_jobs SET \"ExtractionMode\" = 'Structured' WHERE \"ExtractionMode\" IS NULL;");
        migrationBuilder.AlterColumn<string>(
            name: "ExtractionMode",
            table: "document_ocr_jobs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);
        migrationBuilder.AddColumn<string>(
            name: "FullTextContent",
            table: "document_ocr_jobs",
            type: "text",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ArtifactReference",
            table: "document_ocr_jobs",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ExternalContextId",
            table: "document_ocr_jobs",
            type: "character varying(150)",
            maxLength: 150,
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "ReviewedBy",
            table: "document_ocr_jobs",
            type: "uuid",
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ReviewedAt",
            table: "document_ocr_jobs",
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReviewAction",
            table: "document_ocr_jobs",
            type: "text",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReviewComment",
            table: "document_ocr_jobs",
            type: "text",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "OriginalAiNormalizedJson",
            table: "document_ocr_jobs",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ArtifactReference", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "ExternalContextId", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "ExtractionMode", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "FullTextContent", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "OriginalAiNormalizedJson", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "ReviewAction", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "ReviewComment", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "ReviewedAt", table: "document_ocr_jobs");
        migrationBuilder.DropColumn(name: "ReviewedBy", table: "document_ocr_jobs");
    }
}
