-- CreateTable
CREATE TABLE "shipment_approved_quotes" (
    "id" TEXT NOT NULL,
    "tenant_id" TEXT NOT NULL,
    "shipment_id" TEXT NOT NULL,
    "customer_id" TEXT NOT NULL,
    "revision" INTEGER NOT NULL,
    "currency" TEXT NOT NULL DEFAULT 'USD',
    "list_price" DECIMAL(18,2) NOT NULL,
    "floor_price" DECIMAL(18,2) NOT NULL,
    "evidence_reference" TEXT NOT NULL,
    "valid_from" TIMESTAMP(3) NOT NULL,
    "valid_until" TIMESTAMP(3) NOT NULL,
    "status" TEXT NOT NULL DEFAULT 'DRAFT',
    "created_by" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "approved_by" TEXT,
    "approved_at" TIMESTAMP(3),
    "revoked_by" TEXT,
    "revoked_at" TIMESTAMP(3),
    "revoke_reason" TEXT,

    CONSTRAINT "shipment_approved_quotes_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "shipment_approved_quotes_tenant_id_shipment_id_customer_id__idx" ON "shipment_approved_quotes"("tenant_id", "shipment_id", "customer_id", "status");

-- CreateIndex
CREATE UNIQUE INDEX "shipment_approved_quotes_tenant_id_shipment_id_customer_id__key" ON "shipment_approved_quotes"("tenant_id", "shipment_id", "customer_id", "revision");

ALTER TABLE "shipment_approved_quotes"
  ADD CONSTRAINT "shipment_quotes_valid_prices" CHECK ("floor_price" > 0 AND "list_price" >= "floor_price"),
  ADD CONSTRAINT "shipment_quotes_valid_window" CHECK ("valid_from" < "valid_until"),
  ADD CONSTRAINT "shipment_quotes_valid_status" CHECK ("status" IN ('DRAFT', 'APPROVED', 'SUPERSEDED', 'REVOKED'));

CREATE UNIQUE INDEX "shipment_quotes_one_approved_per_shipment_customer"
  ON "shipment_approved_quotes" ("tenant_id", "shipment_id", "customer_id")
  WHERE "status" = 'APPROVED';
