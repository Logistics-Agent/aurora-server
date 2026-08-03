# Logistics Architecture Specification

## Purpose

This file is the cross-service source of truth for Aurora Server logistics services. It defines ownership, boundaries, data flows, and integration rules so each service can evolve independently without cross-service database coupling.

## Service Ownership

### Thanh Tan

* API Gateway & BFF
* Identity & Tenant Service
* Route Planning Agent Service
* AI Ops & Monitoring Service
* Audit Log Service

### Dao Huynh

* Financial & Cost Estimation Service
* Billing & Settlement Service
* Realtime Hub
* Negotiation Agent Service
* Customer Assistant

### Ngoc Khoa

* Shipment Workflow Service
* Notification Service
* GPS Tracking & Monitoring Service
* Document OCR Agent Service
* Regulatory Compliance RAG Service

### Hung Vu

* Email Agent
* Testing
* Documentation

### Minh Huy

* Frontend

## Transportation Service Boundaries

### Route Planning Agent

Answers:

```text
Which route should the shipment take?
```

Owns initial route planning, distance and travel-time calculation, stops, traffic and weather considerations, weight restrictions, alternative routes, and re-routing.

### GPS Tracking and Monitoring

Answers:

```text
Where is the vehicle or shipment now?
```

Owns GPS ingestion, position history, speed, heading, timestamp, vehicle-to-shipment assignment, geofences, signal-loss detection, abnormal-stop detection, and realtime tracking publication.

GPS must not own route planning, route optimization, re-routing, shipment workflow state, cost estimation, or ETA prediction logic.

### Cargo Visibility and ETA

A separate ETA service is not required for the current MVP. GPS provides current tracking data. Route Planning may contain ETA and delay prediction. GPS must not absorb Route Planning responsibilities.

## OCR and Compliance Boundaries

### Document OCR Agent

Owns document type detection, OCR, layout analysis, field extraction, JSON normalization, confidence scoring, and needs-review flags.

OCR does not decide whether cargo complies with regulations.

### Regulatory Compliance RAG

Owns regulation retrieval, import/export restriction checks, dangerous-goods checks, required-document checks, compliance evidence, violations, missing documents, risk level, confidence, and assumptions.

Flow:

```text
Document
→ Document OCR Agent
→ Structured JSON
→ Regulatory Compliance RAG
→ Compliance result
```

OCR and Compliance must not write directly to the Shipment Workflow database.

## Shipment Ownership

```text
Shipment Workflow Service [CORE] — Shipment aggregate owner
```

Shipment Workflow is the single source of truth for Shipment, Cargo, shipment locations, shipment document metadata, shipment milestones, shipment status, shipment lifecycle, tenant ownership, and customer ownership.

Other services must not directly query or update its database. Cross-service communication must use contracts, APIs, integration events, and IDs stored as external references. There must be no cross-service database foreign keys.

## Main Data Flows

### Client Creates Shipment

```text
Frontend
→ API Gateway & BFF
→ Shipment Workflow
→ Shipment database
→ ShipmentCreated event
```

### Staff Imports CSV or Excel

```text
Staff
→ API Gateway
→ Shipment Workflow
→ Validate rows
→ Create Shipment and Cargo
→ Publish events
```

For MVP, small files may be processed synchronously. Large files should use a background import job.

### Create Shipment from Documents

```text
Client or Staff
→ Upload document
→ Document OCR Agent
→ Structured JSON
→ Staff review when confidence is low
→ Shipment Workflow
→ Create or update Shipment
```

### Create Shipment from Email

```text
Incoming email
→ Email Agent
→ Attachments
→ Document OCR Agent
→ Structured data
→ Shipment Workflow
```

### Shipment Event Consumers

Shipment events may be consumed by Route Planning Agent, Financial & Cost Estimation, Regulatory Compliance RAG, Notification Service, Audit Log, Customer Assistant, and GPS Tracking when relevant.

## Full Shipment Workflow Responsibilities

Shipment Workflow must support client-created shipments, staff-created shipments, CSV or Excel import, shipment update, draft deletion, shipment submission, shipment cancellation, shipment list, shipment detail, tenant ownership, customer ownership, cargo management, location management, document metadata management, milestone management, state-machine validation, and integration-event generation.

## Shipment State Machine

MVP state model:

```text
Draft
→ Submitted
→ Planning
→ Negotiating
→ Confirmed
→ PickedUp
→ InTransit
→ Delivered
→ Completed
```

Also support:

```text
CustomsProcessing
Cancelled
```

Cancellation must only be permitted from explicitly allowed states. Clients must not assign arbitrary statuses. All transitions must pass through domain or application validation.

## Shipment Events

Minimum integration events:

* ShipmentCreated
* ShipmentSubmitted
* ShipmentUpdated
* ShipmentCancelled
* ShipmentStatusChanged
* CargoUpdated
* DocumentAttached
* RouteAssigned
* ShipmentPickedUp
* ShipmentDelivered
* ShipmentCompleted

Reliable integration-event publication must use the repository outbox approach.

## Shipment MVP Entities

### Shipment

Minimum fields: Id, ShipmentNumber, TenantId, CustomerId, Status, Priority, TransportMode, RouteId, VehicleId, EstimatedPickupTime, EstimatedDeliveryTime, ActualPickupTime, ActualDeliveryTime, Notes, CreatedBy, CreatedAt, UpdatedAt.

### Cargo

Minimum fields: Id, ShipmentId, Name, Description, HSCode, Quantity, Unit, WeightKg, VolumeM3, DeclaredValue, Currency, IsDangerousGoods, PackageType.

The existing class may currently be named `CargoItem`. Do not rename it automatically merely for naming consistency; inspect compatibility and migration impact first.

### ShipmentLocation

Minimum fields: Id, ShipmentId, Type, Name, Address, Latitude, Longitude, ContactName, ContactPhone, Sequence.

### ShipmentDocument

Minimum fields: Id, ShipmentId, FileName, DocumentType, StorageUrl, OCRStatus, OCRConfidence, UploadedBy, UploadedAt, ExtractedDataJson.

Shipment Workflow stores metadata and references. Actual files should remain in object storage or the repository-defined storage system.

### ShipmentMilestone

Minimum fields: Id, ShipmentId, Status, Description, Latitude, Longitude, RecordedAt, Source, CreatedBy.

GPS owns detailed tracking history. Shipment Workflow stores business milestones only.

## Enums

Required enums: ShipmentStatus, ShipmentPriority, TransportMode, LocationType, DocumentType, OCRStatus, MilestoneSource.

Do not modify implemented enums until code, migration, contracts, and tests have been audited.

## Out-of-Scope MVP Features

Explicitly excluded: billing transactions, payment processing, detailed GPS history in Shipment Workflow, route geometry ownership, full carrier management, warehouse inventory, full customs declaration, insurance policy management, contract management, complex container management, generic workflow engines, and vector databases inside Shipment Workflow.
