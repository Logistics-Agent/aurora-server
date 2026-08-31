# Aurora Server — Implementation Plan

## Current State

Shipment Workflow CreateShipment vertical slice: Completed
Shipment Workflow Aggregate Expansion: Completed
Shipment Workflow full MVP: Completed
Notification Service: FCM popup integration in progress
GPS Tracking and Monitoring: Completed
Document OCR Agent: Completed
Regulatory Compliance RAG: Completed

## Active Work

Active Service: Notification
Active Phase: FCM popup integration and authenticated BFF boundary
Current Branch: codex/api-management-fcm

## Service Progress

| Service | Status |
| --- | --- |
| Shipment Workflow | Completed |
| Notification | FCM popup integration in progress |
| GPS Tracking and Monitoring | Completed |
| Document OCR Agent | Completed |
| Regulatory Compliance RAG | Completed |

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

* `codex/tasks/README.md` (all owned-service phase outputs and gates)
* `codex/tasks/notification/`
* `codex/tasks/gps-tracking/`
* `codex/tasks/document-ocr/`
* `codex/tasks/regulatory-compliance-rag/`

Notification Phases 01-10 are Completed.

## Document OCR Phase Progress

| Phase | Name | Status |
| --- | --- | --- |
| 01 | Project Foundation | Completed |
| 02 | Contracts and API | Completed |
| 03 | Document Job Model | Completed |
| 04 | Persistence | Completed |
| 05 | OCR Provider Abstraction | Completed |
| 06 | Extraction Pipeline | Completed |
| 07 | Retry and Job Processing | Completed |
| 08 | Program and Migration | Completed |
| 09 | Testing | Completed |

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
| 09 | Program and Migration | Completed |
| 10 | Testing and Runtime Validation | Completed |

## Regulatory Compliance RAG Phase Progress

| Phase | Name | Status |
| --- | --- | --- |
| 01 | Project Foundation | Completed |
| 02 | Contracts and API | Completed |
| 03 | Regulatory Document Model | Completed |
| 04 | Ingestion and Chunking | Completed |
| 05 | Embedding and Vector Storage | Completed |
| 06 | Retrieval and Citations | Completed |
| 07 | Compliance Evaluation | Completed |
| 08 | Program Configuration | Completed |
| 09 | Testing | Completed |

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
* Notification Phase 10 completed Shipment lifecycle/document consumption plus GPS alert, Document OCR, and Regulatory Compliance event consumers. Tenant-scoped preferences and inbox idempotency are preserved, and the service passes 41 tests including real RabbitMQ delivery.
* GPS Phase 07 completed circular geofences, abnormal-stop and signal-loss monitoring,
  tenant-scoped management APIs, alert deduplication/resolution, and atomic alert outbox writes.
* GPS Phase 08 completed allowlisted integration-event deserialization, MassTransit publishing,
  PostgreSQL skip-locked outbox batching, bounded retries, and publisher worker behavior.
* GPS Phase 09 configured the complete process, added dedicated local PostgreSQL infrastructure,
  applied `20260721042104_InitialGpsTracking`, and passed startup/Rabbit/worker smoke validation.
* GPS Phase 10 completed PostgreSQL integration, concurrent idempotency, tenant/cascade,
  real RabbitMQ outbox delivery, and full Shipment/Notification regression validation.
* Audited the complete owned-service task map and expanded all Document OCR and Regulatory
  Compliance RAG phases with explicit contracts, boundaries, persistence, provider, migration,
  security, test, runtime, evidence, and per-phase commit criteria.
* Document OCR Phase 01 created the .NET 10 Web/gRPC service, colocated test project, contracts
  assembly, reserved protobuf file, and secret-free configuration skeleton.
* Document OCR Phase 02 defined tenant-safe submit/get/list RPCs, stable enums and timestamps,
  and versioned completed/failed integration-event contracts.
* Document OCR Phase 03 implemented the tenant-owned job aggregate, provider attempts, guarded
  lifecycle transitions, bounded errors/JSON, retry semantics, and inbox/outbox domain records.
* Document OCR Phase 04 configured tenant-filtered PostgreSQL persistence, JSON/confidence fields,
  operational indexes, inbox/outbox storage, and tenant-safe aggregate relationships.
* Document OCR Phase 05 added vendor-neutral OCR/content interfaces, strict document policy,
  bounded provider results/failures, and deterministic local adapters without external access.
* Document OCR Phase 06 completed tenant-safe submit/get/list gRPC behavior and the extraction
  pipeline with stable normalized JSON, confidence/review rules, and atomic completion outbox.
* Document OCR Phase 07 added skip-locked job claims, leases and heartbeats, expired-claim
  recovery, bounded deterministic retries, terminal failure events, and an allowlisted
  transactional outbox publisher; the service now passes 55 tests.
* Document OCR Phase 08 configured the complete service process and dedicated local PostgreSQL,
  applied `20260722035404_InitialDocumentOcr`, and passed HTTP/2, RabbitMQ, worker, build, and
  55-test smoke validation without paid OCR credentials.
* Document OCR Phase 09 added PostgreSQL migration/schema/concurrency/tenant tests, real RabbitMQ
  completion/failure delivery proof, complete gRPC mapping coverage, and final owned-service
  regression. OCR passes 63 tests; Shipment 99, Notification 29, and GPS 50 also pass.
* Regulatory Compliance Phase 01 created the .NET 10 Web/gRPC service, separate contracts
  assembly, colocated tests, reserved protobuf, and secret-free configuration skeleton.
* Regulatory Compliance Phase 02 defined the tenant-safe compliance evaluation, lookup,
  evidence query, and controlled ingestion protobuf API plus versioned completion/failure events.
* Regulatory Compliance Phase 03 modeled platform/tenant regulatory sources, immutable versions
  and chunks, tenant-owned evaluations/findings/citations/traces, inbox/outbox, and EF persistence.
* Regulatory Compliance Phase 04 added authorized, hash-verified source ingestion, deterministic
  chunking with citation metadata, idempotent versioning, and pending-embedding scheduling.
* Regulatory Compliance Phase 05 added provider-neutral deterministic embeddings, bounded
  PostgreSQL `real[]` vector persistence/search, batching, and tenant-safe ranking primitives.
* Regulatory Compliance Phase 06 added effective-date/jurisdiction-aware retrieval, deterministic
  cited evidence, overlap deduplication, evidence sufficiency, trace persistence, and query RPC.
* Regulatory Compliance Phase 07 added idempotent snapshot evaluation, cited deterministic checks,
  risk/confidence/manual review, tenant-safe evaluate/get RPCs, and atomic completion/failure outbox.
* Regulatory Compliance Phase 08 configured the complete process and dedicated PostgreSQL,
  applied `20260723135052_InitialRegulatoryCompliance`, and passed HTTP/2 health, RabbitMQ,
  embedding/outbox worker, vector round-trip, build, and 40-test smoke validation.
* Regulatory Compliance Phase 09 added migrated PostgreSQL full-pipeline, vector/JSON/tenant,
  concurrent idempotency, gRPC mapping, outbox retry/locking, and real RabbitMQ event tests. The
  service passes 51 tests and all owned-service regressions pass sequentially.
* Added a fail-closed local development identity fallback in the shared gRPC auth interceptor.
  The fallback supplies a fixed user and tenant only when both `Development` and
  `DevelopmentIdentity:Enabled` are active; explicit identity metadata remains authoritative,
  partial metadata does not trigger fallback, and non-Development environments ignore it.
* Standardized owned-service local gRPC endpoints as Shipment `6000`, Notification `6001`, GPS
  `6002`, Document OCR `6003`, and Regulatory Compliance `6004`. Each service continues to use its
  separate PostgreSQL database and local port `5433` through `5437`.

## Current Work

Shipment Workflow, Notification, GPS Tracking, Document OCR, and Regulatory Compliance are complete. Shipment publishes
persisted events consumed idempotently by Notification and GPS. Notification also consumes GPS monitoring alerts,
Document OCR completion/failure events, and Regulatory Compliance completion/failure events. GPS, OCR, and Compliance
publish service-owned events through transactional outboxes. Service tests remain colocated under
`src/dotnet/<Service>/Tests`.

Direct local Postman testing can omit tenant and user metadata. The Development identity resolves
to tenant `01920000-0000-7000-8000-000000000001`; Regulatory Compliance additionally receives
only its controlled source-ingestion permissions.

## Blocked Work

No active blocker.

## Remaining Work

Notification FCM integration is authorized and remains active on this branch. The backend contract,
auth boundary, consumers, delivery retry, and secret-safe configuration are implemented. Remaining
gates are reconciling the live legacy Notification database migration, adding the dedicated BFF test
project, frontend Firebase Web popup code in `/home/kaito/project/aurora-client`, and a real runtime
FCM token smoke test. Do not start another service without explicit user or lead authorization.

## Notification FCM continuation evidence (2026-08-30)

```bash
rtk proxy dotnet build src/dotnet/Notification/Notification.csproj --no-restore --nologo -v:minimal
rtk proxy dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj --no-restore --nologo -v:minimal
rtk proxy dotnet restore src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj --nologo
rtk proxy dotnet build src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj --no-restore --nologo -v:minimal
```

Notification build passed with 0 warnings/errors; Notification tests passed 41/41 with the known
NU1903 SQLite advisory; Staff BFF restored and built successfully with existing dependency advisory
warnings. Generated migration `20260830023639_NotificationFcmAudience` was reviewed but not applied:
the running database reports legacy migration `20260719124939_InitialNotification` and a different
legacy schema. Direct Kestrel/RabbitMQ smoke execution is blocked in this sandbox by `SocketException:
Permission denied`; no real FCM popup delivery is claimed.

## Build Results

Latest verified owned-service development-access validation:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet build src/dotnet/Notification/Notification.csproj
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
```

Result: All passed with 0 errors and 0 warnings.

## Test Results

Latest verified Regulatory Compliance Phase 09 and owned-service regression validation:

```bash
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

Result: Regulatory Compliance passed 57 tests, including 6 focused development-identity tests;
Shipment 99; Notification 41; GPS 50; Document OCR 63. Final runs had 0 failures and
0 warnings. All five service processes returned HTTP/2 200 on ports `6000` through `6004` during
the runtime smoke test.

## Migration Results

Initial Shipment Workflow migration `20260713201248_InitialShipmentWorkflow` and expanded MVP migration `20260714042938_ExpandShipmentWorkflowMvp` are applied to local `aurora_shipment_workflow`. Notification migration `20260719124939_InitialNotification` is applied to local `aurora_notification`. GPS migration `20260721042104_InitialGpsTracking` is applied to local `aurora_gps_tracking`. Document OCR migration `20260722035404_InitialDocumentOcr` is applied to local `aurora_document_ocr`. Regulatory Compliance migration `20260723135052_InitialRegulatoryCompliance` is applied to local `aurora_regulatory_compliance`.

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
* Phase 10 owned-service event consumer commit is the current local Notification expansion commit

## Immediate Next Action

Stop on `feat/regulatory-compliance-rag`. Await explicit authorization before creating, merging,
or implementing another service branch.

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

Regulatory Compliance RAG is active on `feat/regulatory-compliance-rag`, stacked from the completed
Document OCR branch. Do not start another service automatically after Compliance completes.
