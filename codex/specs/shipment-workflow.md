# Shipment Workflow Specification

## Purpose

Shipment Workflow is the core service and aggregate owner for shipment lifecycle data and business rules.

## Boundaries

Shipment Workflow owns shipment aggregate state, cargo, shipment locations, document metadata, milestones, status, tenant ownership, customer ownership, and reliable integration-event creation through the outbox.

It does not own route optimization, GPS position history, OCR execution, compliance decisions, notification delivery, billing, settlement, customer chat, or object-file storage.

## Owned Data

Primary MVP entities are Shipment, CargoItem, ShipmentLocation, ShipmentDocument, and ShipmentMilestone. Existing implemented entities also include ShipmentStatusHistory and OutboxMessage.

## Data Not Owned

Shipment Workflow must not store route geometry, detailed GPS telemetry, OCR processing jobs, regulatory vector data, notification delivery attempts, or billing transactions.

## Dependencies

The service depends on shared current-user context, PostgreSQL, gRPC, MediatR, MassTransit/RabbitMQ, Redis through shared services, and Shipment.Contracts for cross-service events.

## Contracts and APIs

The gRPC contract is `protos/shipment_workflow.proto`. Shipment Workflow MVP RPCs for create, get, list, timeline, submit, update, status transitions, cancellation, draft deletion, cargo/location/document/milestone management, and CSV import are implemented.

## Event Consumers and Publishers

Shipment Workflow publishes shipment lifecycle events through outbox records. It may consume route assignment or external updates only through explicit future contracts; direct database reads are forbidden.

## Domain Model

The aggregate must enforce status transitions, validation, cancellation rules, tenant ownership, cargo rules, location rules, document metadata rules, and milestone consistency.

## Persistence

Shipment Workflow owns a PostgreSQL database. There must be no cross-service database foreign keys. All tenant-owned query paths must apply tenant filtering.

## Tenant Behavior

`TenantId` is resolved from authenticated current-user context. Clients must never supply trusted `TenantId`. Missing tenant context must reject writes and must not expose tenant-owned reads.

## Idempotency and Retry

Import, external callbacks, and event consumers should use idempotency keys or event IDs when introduced. Outbox publication must retry and record errors.

## Security

The service must use shared authentication metadata, avoid secrets in source, prevent cross-tenant access, and avoid exposing internal stack traces through gRPC.

## Validation

Validate required shipment fields, cargo fields, location sequence, document metadata, status transitions, cancellation, and import rows.

## Runtime Configuration

Configuration includes PostgreSQL, Redis, RabbitMQ, logging, and authentication/shared service settings. Local Docker infrastructure may provide Postgres, Redis, and RabbitMQ.

## Migration Requirements

Migrations are incremental after the applied initial migration. `20260714042938_ExpandShipmentWorkflowMvp` expands the MVP schema and has been applied to the confirmed local Shipment Workflow database.

## Test Requirements

Tests must cover create, queries, commands, state machine, tenant isolation, validation, cargo/location/document/milestone management, import behavior, outbox records, and migration compatibility.

## Definition of Done

Full Shipment MVP is complete: Create, Get, List, Submit, Update, UpdateStatus, Timeline, Cancel, Draft deletion, cargo/location/document/milestone management, CSV import MVP, outbox events, migrations, and tests pass.

## Assumptions

The existing `CargoItem` name may remain unless a compatibility review justifies renaming. Object storage and detailed GPS history remain outside this service.

## Explicitly Excluded Responsibilities

Billing, payment, detailed GPS history, route planning, OCR execution, compliance decisioning, notification delivery, and vector database ownership are excluded.

## Implementation Status

Shipment Workflow full MVP is completed through Phase 19. Current verified regression: `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` passes and `dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj` passes with 83 tests.
