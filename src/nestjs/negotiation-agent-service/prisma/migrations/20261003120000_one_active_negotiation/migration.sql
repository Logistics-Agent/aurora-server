CREATE UNIQUE INDEX "negotiation_one_active_session_per_shipment_customer"
  ON "negotiation_sessions"("tenant_id", "shipment_id", "customer_id")
  WHERE "status" IN ('OPEN', 'PENDING_APPROVAL');
