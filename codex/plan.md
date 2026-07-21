# Aurora Server — Implementation Plan

## Current State

Shipment Workflow CreateShipment vertical slice: Completed
Shipment Workflow Aggregate Expansion: Completed
Shipment Workflow full MVP: Completed
Notification Service: Completed
GPS Tracking and Monitoring: In Progress
Document OCR Agent: Not Started
Regulatory Compliance RAG: Not Started

## Active Work

Active Service: GPS Tracking and Monitoring
Active Phase: Phase 09 - Program and Migration
Current Branch: feat/gps-tracking

## Service Progress

| Service | Status |
| --- | --- |
| Shipment Workflow | Completed |
| Notification | Completed |
| GPS Tracking and Monitoring | In Progress |
| Document OCR Agent | Not Started |
| Regulatory Compliance RAG | Not Started |

## Shipment Phase Progress

| Phase | Name | Status |
| --- | --- | --- |
| 01 | Project Foundation | Completed |
| 02 | Shipment Contracts | Completed |
| 03 | Domain Model | Completed |
| 04 | Namespace Cleanup | Completed |
| 05 | Persistence | Completed |
| 06 | Proto Contract | Completed |
| 07 | Program Configuration | Completed |
| 08 | Create Shipment Vertical Slice | Completed |
| 09 | Database Migration | Completed |
| 10 | Testing | Completed |
| 11 | Aggregate Expansion | Completed |
| 12 | Workflow State Machine | Completed |
| 13 | Shipment Queries | Completed |
| 14 | Shipment Commands | Completed |
| 15 | Cargo and Location Management | Completed |
| 16 | Document and Milestone Management | Completed |
| 17 | Shipment Import | Completed |
| 18 | Contracts and Integration Events | Completed |
| 19 | Migration and Full MVP Testing | Completed |
| 20 | Outbox Publishing and Notification Integration | Completed |

## Future Service Phase Progress

Future-service phase plans exist under:

* `codex/tasks/notification/`
* `codex/tasks/gps-tracking/`
* `codex/tasks/document-ocr/`
* `codex/tasks/regulatory-compliance-rag/`

Notification Phases 01-09 are Completed.

## GPS Tracking Phase Progress

| Phase | Name | Status |
| --- | --- | --- |
| 01 | Project Foundation | Completed |
| 02 | Contracts and Domain Model | Completed |
| 03 | Persistence | Completed |
| 04 | Location Ingestion | Completed |
| 05 | Current Location and History | Completed |
| 06 | Shipment Event Consumers | Completed |
| 07 | Monitoring Rules | Completed |
| 08 | Realtime Integration | Completed |
| 09 | Program and Migration | In Progress |
| 10 | Testing and Runtime Validation | Not Started |

## Completed Work

* Shipment Workflow project and contract project created.
* CreateShipment gRPC contract, command handler, persistence, migration, and tests are complete.
* Local Shipment Workflow database migration was applied after explicit local DB reset.
* Logistics architecture, service specs, future service plans, Shipment gap analysis, and Shipment Phase 11–19 plans have been documented.
* Phase 11 expanded the Shipment aggregate with MVP shipment fields, ShipmentLocation, ShipmentDocument, ShipmentMilestone, required enums, EF mappings, tenant filters, and aggregate tests.
* Phase 12 implemented the Shipment workflow state machine with validated transitions, terminal states, transition history, milestone creation, and pickup/delivery timestamps.
* Phase 13 implemented tenant-safe GetShipment, ListShipments, and GetShipmentTimeline query behavior and gRPC mapping.
* Phase 14 implemented shipment submit, update, status transition, cancellation, and draft deletion commands with tenant-safe mutation and outbox records.
* Phase 15 implemented tenant-safe cargo and location management commands/RPCs, submit prerequisites, CargoUpdated outbox records, and PostgreSQL-backed tests.
* Phase 16 implemented document metadata commands/RPCs, controlled OCR metadata updates, business milestone creation, DocumentAttached outbox records, timeline regression coverage, and lifecycle history persistence fixes.
* Phase 17 implemented synchronous CSV shipment import with row-level results, limits, TenantId rejection, partial-success transaction policy, and ShipmentCreated outbox writes.
* Phase 18 completed Shipment event contracts with EventId/version fields, added missing lifecycle/update event contracts, and wired outbox writes for submit/update/pickup/delivery/completion flows.
* Phase 19 generated and applied `20260714042938_ExpandShipmentWorkflowMvp`, verified local PostgreSQL schema, ran runtime smoke validation, and completed full Shipment Workflow regression.
* Phase 20 added a PostgreSQL skip-locked outbox publisher for all Shipment event contracts, bounded retry diagnostics, and verified real RabbitMQ delivery into Notification.
* Notification Phases 01-09 completed the standalone service, Shipment event consumers, tenant-safe gRPC APIs, provider-neutral delivery, bounded retry, `20260719124939_InitialNotification`, runtime smoke validation, and 29 passing tests.
* GPS Phase 07 completed circular geofences, abnormal-stop and signal-loss monitoring,
  tenant-scoped management APIs, alert deduplication/resolution, and atomic alert outbox writes.
* GPS Phase 08 completed allowlisted integration-event deserialization, MassTransit publishing,
  PostgreSQL skip-locked outbox batching, bounded retries, and publisher worker behavior.

## Current Work

Shipment Workflow and Notification Service are complete and connected through RabbitMQ. Shipment publishes persisted outbox events with bounded retry; Notification consumes supported events idempotently. Service test projects are colocated under `src/dotnet/<Service>/Tests`; future owned services will follow this layout when their implementation phases begin.

## Blocked Work

No active blocker.

## Remaining Work

No remaining Shipment-to-Notification integration gap. Additional consumers must implement their own inbox/idempotency behavior when their service phases begin.

## Build Results

Latest verified Phase 20 validation:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet build src/dotnet/Notification/Notification.csproj
```

Result: Both passed, 0 errors, 0 warnings.

## Test Results

Latest verified Phase 20 validation:

```bash
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
```

Result: Shipment passed 99 tests; Notification passed 29 tests; 0 warnings.

## Migration Results

Initial Shipment Workflow migration `20260713201248_InitialShipmentWorkflow` and expanded MVP migration `20260714042938_ExpandShipmentWorkflowMvp` are applied to local `aurora_shipment_workflow`. Notification migration `20260719124939_InitialNotification` is applied to local `aurora_notification`.

Part of the validation also produced supporting repository commits:

* `0ebc652` - colocate Shipment and Notification test projects.
* `e574f54` - exclude colocated test artifacts from production Web SDK items.

## Commit History

Recent Notification phase commits:

* `e89550a` — Phase 01 project foundation
* `4baed46` — Phase 02 domain model
* `dfcbdb5` — Phase 03 persistence model
* `52d8456` — Phase 04 Shipment event consumers
* `42a06d2` — Phase 05 notification delivery
* `8f237ff` — Phase 06 retry and idempotency
* `ffb10c9` — Phase 07 program configuration
* `2fc547d` — Phase 08 initial migration
* Phase 09 testing commit is the final local Notification commit

## Immediate Next Action

Complete GPS Tracking Phase 09 on `feat/gps-tracking`. Do not begin Document OCR until
all GPS phases build, migrate, test, run, and are committed.

## Branch Strategy

GPS Tracking was explicitly authorized and its branch was created from the current
`integration` branch before merging the completed Notification branch.

Future stacked branch strategy:

```text
current Shipment branch
└── feat/notification-service
    └── feat/gps-tracking
        └── feat/document-ocr-agent
            └── feat/regulatory-compliance-rag
```

Do not create Document OCR or later branches yet.
