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
                DECLARE
                    column_type text;
                    column_is_nullable boolean;
                BEGIN
                    IF to_regclass('public.shipment_documents') IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT format_type(attribute.atttypid, attribute.atttypmod),
                           NOT attribute.attnotnull
                    INTO column_type, column_is_nullable
                    FROM pg_attribute attribute
                    WHERE attribute.attrelid = 'public.shipment_documents'::regclass
                      AND attribute.attname = 'LastOcrEventId'
                      AND NOT attribute.attisdropped;

                    IF NOT FOUND THEN
                        ALTER TABLE public.shipment_documents ADD COLUMN "LastOcrEventId" uuid NULL;
                        COMMENT ON COLUMN public.shipment_documents."LastOcrEventId"
                            IS 'aurora:migration:AddDocumentOcrProjectionEvent:owned';
                        RETURN;
                    END IF;

                    IF column_type <> 'uuid' THEN
                        RAISE EXCEPTION
                            'shipment_documents.LastOcrEventId has incompatible type %, expected uuid',
                            column_type
                            USING ERRCODE = '42804';
                    END IF;

                    IF NOT column_is_nullable THEN
                        RAISE EXCEPTION
                            'shipment_documents.LastOcrEventId has incompatible nullability, expected nullable uuid'
                            USING ERRCODE = '42804';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    column_comment text;
                BEGIN
                    IF to_regclass('public.shipment_documents') IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT col_description('public.shipment_documents'::regclass, attribute.attnum)
                    INTO column_comment
                    FROM pg_attribute attribute
                    WHERE attribute.attrelid = 'public.shipment_documents'::regclass
                      AND attribute.attname = 'LastOcrEventId'
                      AND NOT attribute.attisdropped;

                    IF FOUND AND column_comment = 'aurora:migration:AddDocumentOcrProjectionEvent:owned' THEN
                        ALTER TABLE public.shipment_documents DROP COLUMN "LastOcrEventId";
                    END IF;
                END $$;
                """);
        }
    }
}
