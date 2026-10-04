ALTER TABLE "shipment_approved_quotes"
  ADD CONSTRAINT "shipment_quotes_usd_only" CHECK ("currency" = 'USD'),
  ADD CONSTRAINT "shipment_quotes_positive_revision" CHECK ("revision" > 0),
  ADD CONSTRAINT "shipment_quotes_evidence_required" CHECK (length(trim("evidence_reference")) > 0),
  ADD CONSTRAINT "shipment_quotes_approval_audit" CHECK (
    "status" NOT IN ('APPROVED', 'SUPERSEDED') OR
    ("approved_by" IS NOT NULL AND "approved_at" IS NOT NULL)
  );
