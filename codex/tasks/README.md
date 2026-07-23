# Service Phase Map

This index explains the output of every owned-service phase. The phase file remains the execution
source of truth for scope, commands, evidence, and completion criteria. Phases are sequential inside
one service; a later service starts only after the current service is built, migrated, tested,
committed, and explicitly authorized.

## Cross-Service Rules

* Each service owns its database; external IDs never become cross-database foreign keys.
* Synchronous integration uses gRPC contracts. Asynchronous integration uses versioned events,
  MassTransit/RabbitMQ, inbox idempotency, and transactional outbox publication.
* TenantId comes from authenticated context or trusted event metadata, never a client field.
* Local tests use deterministic fakes for paid/cloud providers; production providers stay behind
  interfaces and obtain secrets from environment/secret providers.
* A phase is `Completed` only with actual build/test/migration/runtime evidence required by its file.

## Shipment Workflow

Status: Completed. Owns the Shipment aggregate and business workflow.

| Phase | Output | Status |
| --- | --- | --- |
| 01 | Create the .NET service foundation and project layout. | Completed |
| 02 | Define initial Shipment contracts shared with consumers. | Completed |
| 03 | Model Shipment, cargo, status history, and core domain rules. | Completed |
| 04 | Normalize namespaces to the approved Shipment structure. | Completed |
| 05 | Configure EF Core persistence, tenant filters, mappings, and outbox. | Completed |
| 06 | Define the Shipment Workflow gRPC protobuf surface. | Completed |
| 07 | Wire gRPC, shared auth, MediatR, PostgreSQL, and messaging startup. | Completed |
| 08 | Implement the atomic CreateShipment vertical slice. | Completed |
| 09 | Generate and apply the initial Shipment migration. | Completed |
| 10 | Add CreateShipment unit and integration coverage. | Completed |
| 11 | Expand the aggregate with locations, documents, milestones, and MVP fields. | Completed |
| 12 | Enforce the complete Shipment lifecycle state machine. | Completed |
| 13 | Implement tenant-safe detail, list, and timeline queries. | Completed |
| 14 | Implement submit, update, transition, cancel, and draft-delete commands. | Completed |
| 15 | Implement cargo and location add/update/remove/reorder operations. | Completed |
| 16 | Implement document metadata, OCR metadata, milestone, and timeline operations. | Completed |
| 17 | Implement bounded synchronous CSV import with row-level results. | Completed |
| 18 | Complete gRPC contracts and versioned Shipment integration events. | Completed |
| 19 | Apply the expanded MVP migration and complete full regression/runtime validation. | Completed |
| 20 | Publish Shipment outbox events through RabbitMQ with bounded retry. | Completed |

## Notification

Status: Completed. Consumes Shipment events and owns notification delivery state.

| Phase | Output | Status |
| --- | --- | --- |
| 01 | Create the standalone Notification service and colocated tests. | Completed |
| 02 | Model notification messages, recipients, channels, status, and attempts. | Completed |
| 03 | Configure tenant-safe persistence, inbox, and delivery indexes. | Completed |
| 04 | Consume Shipment events idempotently and create local notification records. | Completed |
| 05 | Implement provider-neutral email and in-app delivery. | Completed |
| 06 | Add bounded retry, deduplication, and failure diagnostics. | Completed |
| 07 | Wire gRPC, PostgreSQL, RabbitMQ consumers, providers, and workers. | Completed |
| 08 | Generate/apply the initial Notification migration and smoke-start runtime. | Completed |
| 09 | Complete PostgreSQL, delivery, consumer, tenant, and runtime tests. | Completed |

## GPS Tracking and Monitoring

Status: Completed. Consumes Shipment assignment events and owns vehicle position/monitoring data.

| Phase | Output | Status |
| --- | --- | --- |
| 01 | Create the standalone GPS service, contracts project, and tests. | Completed |
| 02 | Define GPS gRPC/events plus position, assignment, geofence, alert, inbox/outbox models. | Completed |
| 03 | Configure GPS EF mappings, tenant filters, relationships, and indexes. | Completed |
| 04 | Implement idempotent position ingestion, current snapshot, and atomic outbox write. | Completed |
| 05 | Implement tenant-safe current-location and bounded history queries. | Completed |
| 06 | Consume Shipment route/cancel/complete events into an idempotent local projection. | Completed |
| 07 | Implement geofence, abnormal-stop, signal-loss, and alert management rules. | Completed |
| 08 | Publish allowlisted GPS events through the skip-locked transactional outbox. | Completed |
| 09 | Wire runtime, add dedicated PostgreSQL, apply migration, and smoke-start workers. | Completed |
| 10 | Complete PostgreSQL/RabbitMQ integration and full owned-service regression tests. | Completed |

## Document OCR Agent

Status: Completed. Owns extraction jobs/results, not object storage or compliance decisions.

| Phase | Output | Status |
| --- | --- | --- |
| 01 | Create `DocumentOcr`, contracts, configuration skeleton, and colocated tests. | Completed |
| 02 | Define submit/get/list gRPC and completed/failed integration-event contracts. | Completed |
| 03 | Model tenant-owned OCR jobs, attempts, results, confidence, review state, inbox/outbox. | Completed |
| 04 | Configure persistence, restrictive tenant filters, worker/idempotency/outbox indexes. | Completed |
| 05 | Define secure content/OCR provider boundaries and deterministic fakes. | Completed |
| 06 | Implement idempotent extraction, normalization, confidence/review, API, and atomic outbox. | Completed |
| 07 | Add skip-locked workers, leases, bounded retry, terminal failure, and outbox publisher. | Completed |
| 08 | Wire startup, dedicated PostgreSQL, initial migration, Docker, and runtime smoke. | Completed |
| 09 | Complete PostgreSQL/RabbitMQ/security/runtime tests and owned-service regressions. | Completed |

## Regulatory Compliance RAG

Status: In Progress. Owns regulatory sources, retrieval evidence, and compliance evaluations.

| Phase | Output | Status |
| --- | --- | --- |
| 01 | Create `RegulatoryCompliance`, contracts, configuration skeleton, and tests. | Completed |
| 02 | Define evaluation/query/ingestion gRPC and completed/failed event contracts. | Completed |
| 03 | Model and persist source versions, chunks, evaluations, findings, citations, traces, inbox/outbox. | Completed |
| 04 | Implement authorized, idempotent regulatory ingestion and deterministic chunking. | Completed |
| 05 | Implement provider-neutral embeddings and tenant-safe vector persistence/search primitives. | Completed |
| 06 | Implement effective-date-aware retrieval, deterministic ranking, and immutable citations. | Completed |
| 07 | Implement idempotent evidence-backed compliance evaluation and atomic events. | Completed |
| 08 | Wire providers/workers, vector-capable PostgreSQL, migration, health, and runtime smoke. | Completed |
| 09 | Complete deterministic PostgreSQL/vector/RabbitMQ/security/runtime validation. | Not Started |

## Next Gate

The active implementation is Regulatory Compliance RAG Phase 09 on
`feat/regulatory-compliance-rag`. Later phases remain sequentially gated.
