# Shipment Workflow & State Machine Service — Service Overview

> **Service Layer**: Core Freight Lifecycle, State Machine & Orchestration  
> **Target Audience**: Technical Recruiters, Backend Engineers, System Architects  
> **Source-of-Truth**: `src/dotnet/ShipmentWorkflow`, `Shipment.cs`, `ShipmentMilestone.cs`, `ShipmentDocument.cs`, `protos/shipment_workflow.proto`.

---

## 1. Service Purpose & Problem Solved

The shipment lifecycle in freight forwarding spans complex multi-modal stages (booking, pickup, ocean/air freight, customs clearance, last-mile delivery, and proof-of-delivery). Without a central state machine, status updates become inconsistent across tracking, billing, and notification services.

The **Shipment Workflow Service** acts as the **Authoritative Core State Machine**:
- **Finite State Machine (FSM)**: Manages deterministic shipment status transitions (`Draft` $\rightarrow$ `Booked` $\rightarrow$ `Dispatched` $\rightarrow$ `InTransit` $\rightarrow$ `CustomsHold` $\rightarrow$ `OutForDelivery` $\rightarrow$ `Delivered` $\rightarrow$ `Completed`).
- **Multi-Modal Milestones**: Tracks container events, vessel departures, GPS arrival checkpoints, and customs release timestamps.
- **Transactional Event Hub**: Publishes domain events (`ShipmentCreatedEvent`, `ShipmentStatusChangedEvent`, `ShipmentDeliveredEvent`) via the Transactional Outbox pattern to trigger downstream invoicing, GPS tracking, and notifications.

---

## 2. Architecture & Tech Stack

```
[ Frontend SPA / API Gateway ]
              │
              ▼ (gRPC Port 5002)
┌─────────────────────────────────────────────────────────────┐
│                 ShipmentWorkflow Microservice (.NET 10)     │
│  ├── Core Finite State Machine & Transition Guards          │
│  ├── Milestone & Container Tracking Engine                  │
│  ├── Document Attachment Manager (BL, POD, Commercial Inv)  │
│  ├── Status History & Audit Log                             │
│  └── Transactional Outbox (RabbitMQ Publisher)              │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]            [ RabbitMQ ]
     (Shipments, Milestones, Cargo)    (Domain Event Distribution)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Persistence & ORM** | Entity Framework Core 10, PostgreSQL 16 (Neon Serverless SSL) |
| **Messaging & Events** | MassTransit, RabbitMQ, Transactional Outbox Pattern |
| **Concurrency** | Optimistic Concurrency Control (`Version` token) |

---

## 3. Owned Data & Schema Boundaries

The service strictly owns:
- **`Shipments`**: Tracking number, mode (`OceanFCL`, `OceanLCL`, `Air`, `RoadFTL`), status, origin/destination locations, total weight, volume, shipper, and consignee.
- **`CargoItems`**: SKU, package type, dimensions, weight, dangerous goods classification.
- **`ShipmentMilestones`**: Checkpoint names, scheduled vs actual timestamps, latitude/longitude coordinates, and status.
- **`ShipmentDocuments`**: Links to verified OCR documents (Bills of Lading, Customs Declarations, PODs).
- **`ShipmentStatusHistories`**: Immutable log of every status transition, actor ID, and reason.

---

## 4. API & Contract Surface

Exposed via `protos/shipment_workflow.proto` (`ShipmentService`):
- `CreateShipment`: Creates draft or booked shipment.
- `TransitionStatus`: Executes validated FSM state transition (e.g. `InTransit` $\rightarrow$ `CustomsHold`).
- `GetShipment`: Returns shipment details, active milestones, cargo items, and attached documents.
- `ListShipments`: Paginated search with multi-criteria filtering (status, origin, destination, date range).
- `AttachDocument`: Associates an external document ID to the shipment.

---

## 5. Security & Invariants

1. **State Transition Guard**: Illegal state transitions (e.g. `Draft` directly to `Delivered`) throw `InvalidStateTransitionException` (`400 Bad Request`).
2. **Strict Multi-Tenancy**: Isolated PostgreSQL database; all queries filtered by authenticated `TenantId`.
3. **Current Maturity**: Production-ready core operational service with complete FSM transition guards and outbox event streaming.
