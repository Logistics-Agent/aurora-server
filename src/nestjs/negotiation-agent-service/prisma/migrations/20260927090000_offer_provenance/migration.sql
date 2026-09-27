ALTER TABLE "negotiation_messages"
  ADD COLUMN IF NOT EXISTS "customer_offer_price" DOUBLE PRECISION,
  ADD COLUMN IF NOT EXISTS "source_message_id" TEXT;

ALTER TABLE "negotiation_sessions"
  ADD COLUMN IF NOT EXISTS "pricing_evidence_reference" TEXT,
  ADD COLUMN IF NOT EXISTS "pricing_approved_by" TEXT,
  ADD COLUMN IF NOT EXISTS "pricing_approved_at" TIMESTAMP(3);

CREATE UNIQUE INDEX IF NOT EXISTS "negotiation_messages_session_id_source_message_id_key"
  ON "negotiation_messages" ("session_id", "source_message_id");

-- One active negotiation is allowed for a shipment and customer in a tenant.
CREATE UNIQUE INDEX IF NOT EXISTS "negotiation_sessions_one_active_per_customer_shipment"
  ON "negotiation_sessions" ("tenant_id", "shipment_id", "customer_id")
  WHERE "status" IN ('OPEN', 'PENDING_APPROVAL');
