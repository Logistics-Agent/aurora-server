CREATE TABLE "shipment_price_agreements" (
    "id" TEXT NOT NULL,
    "tenant_id" TEXT NOT NULL,
    "shipment_id" TEXT NOT NULL,
    "customer_id" TEXT NOT NULL,
    "subtotal" DECIMAL(18,2) NOT NULL,
    "tax_amount" DECIMAL(18,2) NOT NULL,
    "total_amount" DECIMAL(18,2) NOT NULL,
    "currency" TEXT NOT NULL DEFAULT 'USD',
    "evidence_reference" TEXT NOT NULL,
    "recorded_by" TEXT NOT NULL,
    "recorded_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "shipment_price_agreements_pkey" PRIMARY KEY ("id")
);

CREATE UNIQUE INDEX "shipment_price_agreements_tenant_id_shipment_id_key"
    ON "shipment_price_agreements"("tenant_id", "shipment_id");
CREATE INDEX "shipment_price_agreements_tenant_id_customer_id_idx"
    ON "shipment_price_agreements"("tenant_id", "customer_id");

CREATE TABLE "customer_credit_accounts" (
    "id" TEXT NOT NULL,
    "tenant_id" TEXT NOT NULL,
    "customer_id" TEXT NOT NULL,
    "credit_limit" DECIMAL(18,2) NOT NULL,
    "currency" TEXT NOT NULL DEFAULT 'USD',
    "terms_days" INTEGER NOT NULL,
    "on_hold" BOOLEAN NOT NULL DEFAULT false,
    "approved_by" TEXT NOT NULL,
    "approved_at" TIMESTAMP(3) NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL,
    CONSTRAINT "customer_credit_accounts_pkey" PRIMARY KEY ("id")
);

CREATE UNIQUE INDEX "customer_credit_accounts_tenant_id_customer_id_key"
    ON "customer_credit_accounts"("tenant_id", "customer_id");

CREATE TABLE "customer_credit_limit_revisions" (
    "id" TEXT NOT NULL,
    "account_id" TEXT NOT NULL,
    "credit_limit" DECIMAL(18,2) NOT NULL,
    "terms_days" INTEGER NOT NULL,
    "on_hold" BOOLEAN NOT NULL,
    "approved_by" TEXT NOT NULL,
    "reason" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "customer_credit_limit_revisions_pkey" PRIMARY KEY ("id")
);

CREATE INDEX "customer_credit_limit_revisions_account_id_created_at_idx"
    ON "customer_credit_limit_revisions"("account_id", "created_at");
ALTER TABLE "customer_credit_limit_revisions" ADD CONSTRAINT "customer_credit_limit_revisions_account_id_fkey"
    FOREIGN KEY ("account_id") REFERENCES "customer_credit_accounts"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

ALTER TABLE "invoices" ADD COLUMN "agreement_id" TEXT;
CREATE UNIQUE INDEX "invoices_agreement_id_key" ON "invoices"("agreement_id");
ALTER TABLE "invoices" ADD CONSTRAINT "invoices_agreement_id_fkey"
    FOREIGN KEY ("agreement_id") REFERENCES "shipment_price_agreements"("id") ON DELETE SET NULL ON UPDATE CASCADE;
