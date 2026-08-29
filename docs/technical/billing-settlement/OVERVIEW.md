# Invoicing, Credit & Carrier Settlement Service — Service Overview

> **Service Layer**: Invoicing (AR), Carrier Settlements (AP) & Credit Management  
> **Target Audience**: Technical Recruiters, Fintech Engineers, System Architects  
> **Source-of-Truth**: `src/nestjs/billing-service`, `BillingService`, `prisma/schema.prisma`.

---

## 1. Service Purpose & Problem Solved

Freight forwarding cash flow requires orchestrating two distinct financial streams: invoicing shippers (Accounts Receivable - AR) and settling carrier freight charges (Accounts Payable - AP). Missing Proof-of-Delivery documents causes delayed billing, while exceeding credit limits leads to bad debt.

The **Billing & Settlement Service** provides **Automated Invoicing + Credit Limit Enforcement + Carrier Freight Settlement**:
- **Event-Driven Invoicing**: Consumes `ShipmentDeliveredEvent` from RabbitMQ and automatically compiles billable items, customs duties, and surcharges into an issued invoice.
- **Credit Limit & Terms Control**: Tracks customer credit exposure against limits (Net 15, Net 30, COD); automatically blocks new booking dispatches if overdue limits are breached.
- **Carrier Settlement & Reconciliation**: Matches inbound carrier invoices against original purchase orders, flags freight discrepancies, and processes settlement batches.
- **Payment Lifecycle & Webhooks**: Reconciles incoming bank transfers and payment gateway webhooks (Stripe).

---

## 2. Architecture & Tech Stack

```
[ ShipmentWorkflow (Delivered Event) ] ──(RabbitMQ)──┐
                                                     ▼
┌─────────────────────────────────────────────────────────────┐
│                 Billing & Settlement Microservice (NestJS)  │
│  ├── Invoicing Engine (AR: Draft -> Issued -> Paid)         │
│  ├── Credit Limit & Aging Evaluator (Net 30, Overdue)       │
│  ├── Carrier Settlement Reconciler (AP)                     │
│  ├── Payment Gateway Ingestion & Webhook Handler            │
│  └── Transactional Outbox (RabbitMQ Publisher)              │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]           [ RabbitMQ ]
    (Invoices, Settlements, Payments)  (InvoicePaidEvent, CreditHold)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | Node.js 20, NestJS 10, TypeScript |
| **Persistence & ORM** | Prisma ORM, PostgreSQL 16 (Neon Serverless SSL) |
| **Messaging & Events** | RabbitMQ (`InvoiceIssuedEvent`, `InvoicePaidEvent`, `CreditLimitExceededEvent`) |
| **BFF Client** | `Staff.Bff` (`/api/v1/billing/*`), `Admin.Bff` |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`Invoices`**: Invoice number, `TenantId`, `CustomerId`, `ShipmentId`, total amount, currency, tax, status (`Draft`, `Issued`, `PartiallyPaid`, `Paid`, `Overdue`, `Void`), due date.
- **`InvoiceLineItems`**: Description, quantity, unit rate, tax rate, and total price.
- **`CarrierSettlements`**: Carrier ID, invoice reference, approved amount, discrepancy status, and settlement batch ID.
- **`Payments`**: Payment method, transaction reference, amount paid, and allocation to invoices.
- **`CustomerCreditAccounts`**: Approved credit limit, current outstanding balance, credit terms (e.g. Net 30), and credit hold flag.

---

## 4. API & Contract Surface

Exposed endpoints:
- `POST /api/v1/billing/invoices`: Creates manual or draft invoice.
- `GET /api/v1/billing/invoices/{id}`: Retrieves invoice details and payment history.
- `POST /api/v1/billing/payments`: Records a payment and updates invoice balance.
- `GET /api/v1/billing/customers/{id}/credit-status`: Returns outstanding exposure and credit hold status.
- `POST /api/v1/billing/settlements/reconcile`: Reconciles carrier invoice against rate card.

---

## 5. Security & Invariants

1. **Credit Lock Invariant**: If a customer's unpaid balance exceeds their credit limit, the service emits `CreditLimitExceededEvent` to pause new booking dispatch.
2. **Immutable Financial History**: Invoices in `Paid` status cannot be mutated; adjustments require credit notes.
3. **Current Maturity**: Production-ready AR invoicing, payment recording, and credit terms engine.
