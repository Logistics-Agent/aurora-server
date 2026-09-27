-- CreateTable
CREATE TABLE "negotiation_sessions" (
    "id" TEXT NOT NULL,
    "tenant_id" TEXT NOT NULL,
    "shipment_id" TEXT NOT NULL,
    "customer_id" TEXT NOT NULL,
    "status" TEXT NOT NULL DEFAULT 'OPEN',
    "current_round" INTEGER NOT NULL DEFAULT 1,
    "max_rounds" INTEGER NOT NULL DEFAULT 5,
    "list_price" DOUBLE PRECISION NOT NULL,
    "bottom_price" DOUBLE PRECISION NOT NULL,
    "currency" TEXT NOT NULL DEFAULT 'USD',
    "suggested_subject" TEXT,
    "suggested_body" TEXT,
    "suggested_language" TEXT DEFAULT 'en',
    "suggested_reply_available" BOOLEAN NOT NULL DEFAULT false,
    "ai_draft_used" BOOLEAN NOT NULL DEFAULT false,
    "fallback_used" BOOLEAN NOT NULL DEFAULT false,
    "last_decision" TEXT,
    "last_approved_amount" DOUBLE PRECISION,
    "source_message_id" TEXT,
    "source_thread_id" TEXT,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "negotiation_sessions_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "negotiation_messages" (
    "id" TEXT NOT NULL,
    "session_id" TEXT NOT NULL,
    "round" INTEGER NOT NULL DEFAULT 1,
    "sender" TEXT NOT NULL,
    "message" TEXT NOT NULL,
    "offer_price" DOUBLE PRECISION,
    "decision" TEXT,
    "currency" TEXT NOT NULL DEFAULT 'USD',
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "negotiation_messages_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "negotiation_sessions_tenant_id_idx" ON "negotiation_sessions"("tenant_id");

-- CreateIndex
CREATE INDEX "negotiation_sessions_shipment_id_idx" ON "negotiation_sessions"("shipment_id");

-- CreateIndex
CREATE INDEX "negotiation_sessions_customer_id_idx" ON "negotiation_sessions"("customer_id");

-- CreateIndex
CREATE INDEX "negotiation_messages_session_id_idx" ON "negotiation_messages"("session_id");

-- AddForeignKey
ALTER TABLE "negotiation_messages" ADD CONSTRAINT "negotiation_messages_session_id_fkey" FOREIGN KEY ("session_id") REFERENCES "negotiation_sessions"("id") ON DELETE CASCADE ON UPDATE CASCADE;
