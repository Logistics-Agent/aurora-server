# Phase 09 — Testing

## Status

Completed

## Goal

Add Compliance tests.

## Prerequisites

Phase 08.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Phases 01-08 are implemented with 40 passing unit/model tests, an applied PostgreSQL migration,
and a successful local runtime smoke. Phase 09 closes integration, concurrency, broker, gRPC,
security, and owned-service regression coverage.

## Scope

Audit and complete domain, contract, PostgreSQL/vector, retrieval, evaluation, messaging, security,
and runtime coverage for the full Regulatory Compliance RAG MVP.

## Required Behavior

* Cover source/version domain rules, deterministic chunking, embedding validation/idempotency,
  retrieval filters/ranking, citation construction, evaluation rules, and insufficient evidence.
* Add PostgreSQL-backed tests for migration/vector extension, source visibility, unique hashes,
  effective/superseded versions, relationships, JSON/vector persistence, inbox/outbox locking.
* Test all gRPC mappings, missing/client TenantId behavior, cross-tenant access, staff ingestion
  authorization, input/size/topK limits, and unsafe source reference rejection.
* Run deterministic end-to-end ingestion -> embedding -> retrieval -> cited evaluation with fakes.
* Prove concurrent/replayed ingestion and evaluation are idempotent and partial failures are atomic.
* With RabbitMQ available, publish/receive completion/failure events and verify EventId/TenantId plus
  outbox processing state; no OCR/Shipment/Notification process is required.
* Smoke-start with Docker and fake providers. No test may require paid AI credentials or network
  access to live regulatory sources.
* Rebuild/rerun Shipment, Notification, GPS, and Document OCR regressions when OCR exists; record
  unrelated failures rather than weakening another service.
* Inspect migration state, full diff, secrets, generated artifacts, and working tree.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj
dotnet ef migrations list --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Full service definition of done passes with test count, migration/vector, runtime, and broker proof.
* Create local commit `test(compliance): complete service validation`.

## Work Log

### Completed

* Added a dedicated migrated PostgreSQL test database fixture on the Compliance database server.
* Added a deterministic end-to-end ingestion -> chunking -> embedding -> vector retrieval -> cited
  compliance evaluation test with JSON/vector persistence and tenant isolation assertions.
* Added concurrent ingestion/evaluation tests. They exposed race-time unique violations; production
  code now safely reloads the committed winner for matching idempotent requests and rejects changed
  payloads without duplicating sources, evaluations, traces, or outbox messages.
* Added concurrent PostgreSQL outbox locking coverage and real RabbitMQ delivery tests for both
  completion and failure events, including EventId/TenantId and processed-state assertions.
* Added successful mapping tests for all four gRPC methods and Unauthenticated mapping for missing
  tenant context after valid transport input reaches the application boundary.
* Added runtime option bounds, allowlisted event deserialization, and bounded outbox retry tests.
* Re-ran the complete Compliance suite and all Shipment, Notification, GPS, and OCR regressions.
* Repeated startup smoke with deterministic providers and healthy Docker dependencies; health,
  worker queries, RabbitMQ bus startup, and graceful shutdown passed.

### Files Changed

* `src/dotnet/RegulatoryCompliance/Application/Ingestion/RegulatoryIngestionService.cs`
* `src/dotnet/RegulatoryCompliance/Application/Evaluations/ComplianceEvaluationService.cs`
* `src/dotnet/RegulatoryCompliance/Tests/Integration/RegulatoryCompliancePostgresCollection.cs`
* `src/dotnet/RegulatoryCompliance/Tests/Integration/RegulatoryCompliancePostgresIntegrationTests.cs`
* `src/dotnet/RegulatoryCompliance/Tests/Grpc/TestServerCallContext.cs`
* `src/dotnet/RegulatoryCompliance/Tests/Grpc/RegulatoryComplianceGrpcServiceTests.cs`
* `src/dotnet/RegulatoryCompliance/Tests/Infrastructure/ComplianceRuntimeTests.cs`
* `codex/tasks/regulatory-compliance-rag/phase-09-testing.md`
* `codex/tasks/README.md`
* `codex/specs/regulatory-compliance-rag.md`
* `codex/plan.md`

### Commands Executed

```bash
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj --no-restore
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj --no-restore --filter FullyQualifiedName~RegulatoryComplianceGrpcServiceTests
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet ef migrations list --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj -- --environment Development
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --no-restore
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj --no-restore
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj --no-restore
docker compose -f docker-compose.dev.yml ps
dotnet run --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --no-build --launch-profile http
curl --http2-prior-knowledge --fail http://localhost:5093/health
git diff --check
git status --short
```

### Build Result

Passed: Regulatory Compliance and its two referenced projects built with 0 errors and 0 warnings.

### Test Result

Passed: Compliance 51, Shipment 99, Notification 29, GPS 50, Document OCR 63; 0 failed and
0 warnings in the final runs. An initial parallel Shipment/Notification regression attempt caused
`CS2012` artifact contention, and a subsequent Shipment run exposed its existing shared-test-DB
race (`3D000`/`57P01`, 97 passed). Sequential clean reruns passed 99/99 and 29/29 respectively;
no out-of-scope production code was changed.

### Runtime Result

Passed. Docker showed all five owned-service PostgreSQL containers plus Redis and RabbitMQ healthy.
Compliance listened on `http://localhost:5093`; HTTP/2 `/health` returned `Healthy`; logs showed
the RabbitMQ bus and embedding/outbox workers active. `SIGTERM` stopped the application and bus
cleanly. Automated tests used deterministic local providers and no paid credentials/network source.

### Migration Result

Passed. `20260723135052_InitialRegulatoryCompliance` remains the only Compliance migration and is
applied to both the confirmed local development database and the isolated test database. PostgreSQL
integration tests validated schema relationships, `jsonb`, `real[]`, tenant filters, unique
idempotency constraints, migrations, and skip-locked outbox behavior.

### Remaining Issues

No blocker. A production cloud embedding/evaluation adapter is intentionally outside this local MVP;
the provider boundaries and validated configuration are ready for deployment-owned credentials.
