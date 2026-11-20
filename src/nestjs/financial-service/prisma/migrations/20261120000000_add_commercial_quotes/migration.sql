-- CreateTable
CREATE TABLE IF NOT EXISTS "commercial_quotes" (
    "id" TEXT NOT NULL,
    "tenant_id" TEXT NOT NULL,
    "source_thread_id" TEXT,
    "shipment_id" TEXT NOT NULL,
    "customer_id" TEXT,
    "route_id" TEXT,
    "route_name" TEXT,
    "distance_km" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "origin_country" TEXT NOT NULL DEFAULT '',
    "origin_port" TEXT NOT NULL,
    "destination_country" TEXT NOT NULL DEFAULT '',
    "destination_port" TEXT NOT NULL,
    "weight_kg" DOUBLE PRECISION NOT NULL,
    "volume_cbm" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "transport_mode" TEXT NOT NULL DEFAULT 'SEA',
    "cargo_type" TEXT NOT NULL DEFAULT 'GENERAL',
    "cargo_value" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "hs_codes" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "base_freight_cost" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "port_handling_fees" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "fuel_surcharge_fee" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "cargo_insurance_fee" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "total_customs_fee" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "cost_price" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "min_acceptable_rate" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "target_rate" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "agreed_price" DOUBLE PRECISION,
    "currency" TEXT NOT NULL DEFAULT 'USD',
    "calculation_method" TEXT NOT NULL DEFAULT 'PENDING_ROUTE',
    "description" TEXT,
    "status" TEXT NOT NULL DEFAULT 'PENDING_ROUTE',
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "commercial_quotes_pkey" PRIMARY KEY ("id")
);

-- CreateIndex (1 quote row per shipment per tenant; non-unique source_thread_id allows 1 thread -> N shipments)
CREATE UNIQUE INDEX IF NOT EXISTS "commercial_quotes_tenant_id_shipment_id_key" ON "commercial_quotes"("tenant_id", "shipment_id");
CREATE INDEX IF NOT EXISTS "commercial_quotes_tenant_id_idx" ON "commercial_quotes"("tenant_id");
CREATE INDEX IF NOT EXISTS "commercial_quotes_tenant_id_source_thread_id_idx" ON "commercial_quotes"("tenant_id", "source_thread_id");
