# Aurora Server — Requirements

## Project Overview

`aurora-server` is the backend for a multi-tenant logistics SaaS platform. The platform manages shipment creation, route planning, GPS tracking, document processing, regulatory compliance, shipment status, notifications, and cost/settlement workflows.

The cross-service source of truth is `codex/specs/logistics-architecture.md`.

## Architecture

Aurora follows a microservice architecture. Each service must run independently, own a separate database, be independently deployable, and communicate through gRPC or integration events.

Services must never directly query or update another service database. Cross-service relationships are represented by external IDs, contracts, APIs, and events. Cross-service database foreign keys are forbidden.

## Technology Stack

* .NET 10
* ASP.NET Core
* gRPC
* Protocol Buffers
* Entity Framework Core
* PostgreSQL
* MediatR/CQRS
* MassTransit
* RabbitMQ
* Redis
* AWS Cognito
* YARP API Gateway

Service test projects are colocated under `src/dotnet/<Service>/Tests`. Future service test projects are created with their active implementation phase rather than scaffolded in advance.

## Assigned Services

Ngoc Khoa owns:

* Shipment Workflow Service
* Notification Service
* GPS Tracking and Monitoring Service
* Document OCR Agent Service
* Regulatory Compliance RAG Service

Implementation order remains Shipment Workflow first, then Notification, GPS Tracking, Document OCR, and Regulatory Compliance RAG.

## Shipment Workflow Ownership

Shipment Workflow is the Shipment aggregate owner and source of truth for Shipment, Cargo, Shipment locations, Shipment document metadata, Shipment milestones, Shipment status, tenant ownership, and customer ownership.

Current implemented state:

* Full Shipment Workflow MVP: Completed.
* Shipment Workflow migrations are applied locally.
* Latest verified Shipment Workflow regression: 99 tests passing.

Notification Service is completed with its initial migration applied locally and 29 tests passing. GPS Tracking is the next planned service but is not active until explicitly authorized.

## Tenant Isolation

Tenant-owned entities must contain or inherit tenant ownership. Tenant access is resolved from authentication metadata, access-token claims, or current-user context. The backend must not trust client-supplied `TenantId`.

## Integration Events

Reliable event publication uses the outbox pattern. Shipment Workflow persists versioned
Shipment events and its background publisher sends pending rows through MassTransit and
RabbitMQ with bounded retry. Consumers remain responsible for idempotency and must not read
the Shipment database as a workaround.

## Documentation Map

* Cross-service architecture: `codex/specs/logistics-architecture.md`
* Shipment Workflow spec: `codex/specs/shipment-workflow.md`
* Future service specs: `codex/specs/notification.md`, `codex/specs/gps-tracking.md`, `codex/specs/document-ocr.md`, `codex/specs/regulatory-compliance-rag.md`
* Active execution plan: `codex/plan.md`
