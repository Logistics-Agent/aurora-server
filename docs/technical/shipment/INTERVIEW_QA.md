# Shipment Workflow & State Machine Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in .NET 10 `ShipmentWorkflow` implementation.

---

### Q1 (Junior): Why use a Finite State Machine (FSM) for shipment management?
**Answer**:  
Freight shipments follow strict operational dependencies (e.g., cargo cannot be marked `Delivered` before being `Dispatched` or `InTransit`). An FSM prevents illegal state jumps, ensures every state change validates prerequisite milestones, and provides a clear audit trail of who changed the status, when, and why.

---

### Q2 (Mid): How does the Transactional Outbox pattern prevent message loss during shipment delivery?
**Answer**:  
When a shipment is marked `Delivered`, the database transaction executes two operations atomically:
1. Updates `shipments` table (`status = 'Delivered'`).
2. Inserts a record into `outbox_messages` with payload `ShipmentDeliveredEvent`.
If the message broker (RabbitMQ) is down at that exact second, the event is safely persisted in the database. The background outbox processor retries publishing until the broker acknowledges receipt, guaranteeing **At-Least-Once Delivery**.

---

### Q3 (Mid): How does the service handle race conditions when multiple milestone updates arrive concurrently?
**Answer**:  
The service uses **Optimistic Concurrency Control** via a `Version` integer column on `Shipment`. If two GPS telematics events try to update different milestones at the same instant, EF Core detects a version mismatch on the second commit and throws `DbUpdateConcurrencyException`, triggering a clean reload and retry.

---

### Q4 (Senior): How does `ShipmentWorkflow` coordinate with `BillingService` when a shipment completes?
**Answer**:  
The integration is purely asynchronous and event-driven:
1. Upon reaching `Delivered`, `ShipmentWorkflow` emits `ShipmentDeliveredEvent` to RabbitMQ.
2. `BillingService` consumes this event, verifies the attached Proof-of-Delivery (POD) document, and automatically generates an invoice draft in its own database.
3. Neither service makes synchronous cross-database queries or direct HTTP dependencies during status transitions.

---

### Q5 (System Design): What are the tradeoffs of storing milestones within the core shipment database vs. a dedicated tracking service?
**Answer**:  
- **Pros**: Strong ACID consistency between shipment status and milestone progress, simplified querying for customer tracking APIs, and single-database backup management.
- **Cons**: High-frequency telematics pings (e.g. 5-second GPS pings) would overload the relational shipment database; therefore, raw high-frequency telemetry is handled by the dedicated `GpsTracking` service, which emits milestone breach events only when meaningful geographic checkpoints are reached.
