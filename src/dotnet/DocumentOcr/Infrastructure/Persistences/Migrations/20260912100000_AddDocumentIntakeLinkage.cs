using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentOcr.Infrastructure.Persistences.Migrations;

public partial class AddDocumentIntakeLinkage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "UploadId",
            table: "document_ocr_jobs",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_document_ocr_jobs_TenantId_UploadId",
            table: "document_ocr_jobs",
            columns: new[] { "TenantId", "UploadId" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_document_ocr_jobs_document_upload_sessions_TenantId_UploadId",
            table: "document_ocr_jobs",
            columns: new[] { "TenantId", "UploadId" },
            principalTable: "document_upload_sessions",
            principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_document_ocr_jobs_document_upload_sessions_TenantId_UploadId",
            table: "document_ocr_jobs");

        migrationBuilder.DropIndex(
            name: "IX_document_ocr_jobs_TenantId_UploadId",
            table: "document_ocr_jobs");

        migrationBuilder.DropColumn(
            name: "UploadId",
            table: "document_ocr_jobs");
    }
}
