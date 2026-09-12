using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipmentWorkflow.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOcrProjectionEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastOcrEventId",
                table: "shipment_documents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastOcrEventId",
                table: "shipment_documents");
        }
    }
}
