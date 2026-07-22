# Phase 09 — Testing

## Status

Completed

## Goal

Add OCR tests.

## Prerequisites

Phase 08.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Audit and complete domain, application, contract, PostgreSQL, messaging, security, and runtime
coverage for the full Document OCR MVP, then verify owned-service regressions.

## Required Behavior

* Cover domain transitions, validation, confidence/review rules, normalization, provider error
  classification, retries, leases, cancellation, idempotency, and tenant isolation.
* Add PostgreSQL-backed tests for migration schema, unique keys, restrictive missing-tenant filter,
  concurrent job claim, JSON persistence, relationships, and outbox locking.
* Test Submit/Get/List gRPC mappings, no client TenantId, cross-tenant NotFound behavior, document
  limits, unsupported formats, and unsafe storage reference rejection.
* Test deterministic fake extraction end-to-end from submission through completed result/outbox.
* With RabbitMQ available, prove one completion event and one permanent-failure event are delivered
  with matching EventId/TenantId and marked processed.
* Smoke-start the service with Docker dependencies; tests must not need paid provider credentials.
* Rebuild/rerun Shipment, Notification, and GPS regressions and document unrelated failures without
  modifying those services unless OCR caused the regression.
* Inspect migration state, complete diff, secrets, generated artifacts, and working tree.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
dotnet ef migrations list --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Full OCR definition of done passes with actual test count, migration, runtime, and broker evidence.
* Create local commit `test(ocr): complete service validation`.

## Work Log

### Completed

Completed the full Document OCR MVP validation. Added PostgreSQL-backed coverage for migrated
schema behavior, JSON persistence, tenant filters, aggregate cascade, concurrent idempotent
submission, skip-locked job claiming, and concurrent outbox locking. Added real RabbitMQ proof
for both completion and permanent-failure events, plus missing Get/List gRPC mapping coverage.
Revalidated runtime startup and every previously completed owned service.

### Files Changed

* `src/dotnet/DocumentOcr/Tests/Grpc/DocumentOcrGrpcServiceTests.cs`
* `src/dotnet/DocumentOcr/Tests/Integration/DocumentOcrPostgresCollection.cs`
* `src/dotnet/DocumentOcr/Tests/Integration/DocumentOcrPostgresIntegrationTests.cs`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj --filter "FullyQualifiedName~DocumentOcrGrpcServiceTests|FullyQualifiedName~DocumentOcrPostgresIntegrationTests"
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
dotnet ef migrations list --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --no-restore
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj --no-restore
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore
dotnet run --no-build --project src/dotnet/DocumentOcr/DocumentOcr.csproj --launch-profile http
curl --http2-prior-knowledge --max-time 5 http://localhost:5092/
docker compose -f docker-compose.dev.yml ps
docker exec aurora-rabbitmq rabbitmqctl list_connections name peer_host peer_port state
git diff --check
```

### Build Result

Passed. Document OCR, Shipment Workflow, Notification, and GPS Tracking all built with 0 errors
and 0 warnings.

### Test Result

Passed: Document OCR 63, Shipment Workflow 99, Notification 29, and GPS Tracking 50 tests.
The first Shipment regression attempt had 2 failures with PostgreSQL `57P01` because several
accidentally overlapping test processes concurrently deleted the same test database. After all
processes exited, one isolated rerun passed 99/99; no production or Shipment test code changed.
Real RabbitMQ delivery preserved EventId/TenantId for one completed and one failed OCR event, and
both corresponding PostgreSQL outbox records were marked processed.

### Runtime Result

Passed. Document OCR started on port 5092, returned `Document OCR gRPC Service` over HTTP/2,
connected to RabbitMQ, and stopped cleanly. Dedicated OCR PostgreSQL, Redis, and RabbitMQ were
healthy. No paid provider credential or external service process was required.

### Migration Result

`20260722035404_InitialDocumentOcr` remains applied to `aurora_document_ocr` with no pending
migration. The PostgreSQL test database was created separately and migrated from the committed
initial migration before integration tests.

### Remaining Issues

None. Paid OCR vendor adapters remain intentionally outside this deterministic MVP boundary.
