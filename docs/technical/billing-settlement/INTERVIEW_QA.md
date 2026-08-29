# Invoicing, Credit & Carrier Settlement Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & Fintech System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in NestJS `billing-service` implementation.

---

### Q1 (Junior): What triggers the creation of an invoice in Aurora?
**Answer**:  
Invoice creation is event-driven. When `ShipmentWorkflow` marks a shipment as `Delivered` upon customer receipt of the Proof-of-Delivery (POD), it emits a `ShipmentDeliveredEvent` to RabbitMQ. The `BillingService` consumes this event and compiles all verified freight line items, customs duties, and surcharges into an invoice draft for review and issuance.

---

### Q2 (Mid): How does the credit limit enforcement mechanism prevent bad debt?
**Answer**:  
The service evaluates real-time credit exposure: it sums all unpaid balances across active invoices (`Issued`, `PartiallyPaid`, `Overdue`) for a customer. If the outstanding total exceeds their approved `CreditLimit` or if any invoice has passed its due date (`hasOverdueInvoices = true`), the customer is placed on `CreditHold`, and the service emits `CreditLimitExceededEvent` to pause new booking dispatches.

---

### Q3 (Mid): How are partial payments allocated across invoice line items?
**Answer**:  
When a partial payment is recorded, the service creates a `Payment` record and updates `Invoice.PaidAmount` and `Invoice.RemainingBalance`. Status transitions to `PartiallyPaid`. If subsequent payments reduce `RemainingBalance` to zero, the invoice transitions to `Paid` and emits `InvoicePaidEvent`.

---

### Q4 (Senior): How does the service reconcile carrier invoices against original purchase orders?
**Answer**:  
The service executes automated **3-Way Matching**:
1. It compares the carrier's billed amount against the original shipment rate card stored in `FinancialService`.
2. If the variance is within the approved tolerance threshold ($\le \pm 2\%$), the settlement is auto-approved and queued into the next carrier payout batch.
3. If an unexpected charge (e.g. storage or detention fee) causes variance $> 2\%$, the settlement transitions to `REVIEW_REQUIRED`, blocking payment until accounts payable staff verifies supporting carrier receipts.

---

### Q5 (System Design): What are the tradeoffs of handling payments and invoicing in NestJS vs. a monolithic ERP?
**Answer**:  
- **Pros**: Microservice separation enables high-throughput event consumption from IoT delivery checkpoints, real-time credit checks during booking creation, and rapid integration with modern payment gateways (Stripe) and webhooks without waiting on heavy ERP batch jobs.
- **Cons**: Requires building custom invoice aging schedulers and double-entry accounting reconciliation jobs, which Aurora maintains via Prisma transactions and audit tables.
