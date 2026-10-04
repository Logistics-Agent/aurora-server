ALTER TABLE "negotiation_sessions"
  ADD COLUMN "quote_id" TEXT,
  ADD COLUMN "quote_revision" INTEGER,
  ADD COLUMN "quote_valid_until" TIMESTAMP(3);

CREATE INDEX "negotiation_sessions_quote_id_idx" ON "negotiation_sessions"("quote_id");
