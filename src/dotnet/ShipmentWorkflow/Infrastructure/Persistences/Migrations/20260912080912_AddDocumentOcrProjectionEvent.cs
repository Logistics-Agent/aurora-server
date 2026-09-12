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
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.shipment_documents') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'shipment_documents'
                             AND column_name = 'LastOcrEventId') THEN
                        ALTER TABLE "shipment_documents" ADD COLUMN "LastOcrEventId" uuid;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.shipment_documents') IS NOT NULL
                       AND EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'shipment_documents'
                             AND column_name = 'LastOcrEventId') THEN
                        ALTER TABLE "shipment_documents" DROP COLUMN "LastOcrEventId";
                    END IF;
                END $$;
                """);
        }
    }
}
