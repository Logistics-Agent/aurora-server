-- AlterTable: add optional quote_id linking to CommercialQuote while keeping shipment_id NOT NULL
ALTER TABLE "negotiation_sessions" ADD COLUMN IF NOT EXISTS "quote_id" TEXT;

-- CreateIndex (non-unique so 1 mail thread can hold multiple negotiation sessions across different shipments)
CREATE INDEX IF NOT EXISTS "negotiation_sessions_tenant_id_source_thread_id_idx" ON "negotiation_sessions"("tenant_id", "source_thread_id");
CREATE INDEX IF NOT EXISTS "negotiation_sessions_tenant_id_quote_id_idx" ON "negotiation_sessions"("tenant_id", "quote_id");
