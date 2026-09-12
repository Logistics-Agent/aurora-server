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
                ALTER TABLE IF EXISTS public.shipment_documents
                    ADD COLUMN IF NOT EXISTS "LastOcrEventId" uuid NULL;

                DROP TABLE IF EXISTS pg_temp.aurora_document_ocr_projection_validation;

                CREATE TEMP TABLE aurora_document_ocr_projection_validation
                ON COMMIT DROP
                AS
                WITH column_shape AS (
                    SELECT format_type(attribute.atttypid, attribute.atttypmod) AS column_type,
                           NOT attribute.attnotnull AS column_is_nullable
                    FROM pg_attribute attribute
                    WHERE attribute.attrelid = to_regclass('public.shipment_documents')
                      AND attribute.attname = 'LastOcrEventId'
                      AND NOT attribute.attisdropped
                )
                SELECT CASE
                    WHEN to_regclass('public.shipment_documents') IS NULL THEN NULL::uuid
                    WHEN EXISTS (SELECT 1 FROM column_shape WHERE column_type <> 'uuid') THEN
                        (
                            SELECT 'shipment_documents.LastOcrEventId has incompatible type '
                                || column_type
                                || ', expected uuid'
                            FROM column_shape
                            LIMIT 1
                        )::uuid
                    WHEN EXISTS (SELECT 1 FROM column_shape WHERE NOT column_is_nullable) THEN
                        (
                            SELECT 'shipment_documents.LastOcrEventId has incompatible nullability, expected nullable uuid'
                            FROM column_shape
                            LIMIT 1
                        )::uuid
                    ELSE NULL::uuid
                END AS validation_result;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- This additive projection column may pre-date this migration or be used by a
                -- later deployment. Keep Down data-preserving and never infer ownership from a
                -- mutable PostgreSQL comment.
                DROP TABLE IF EXISTS pg_temp.aurora_document_ocr_projection_down_noop;
                """);
        }
    }
}
