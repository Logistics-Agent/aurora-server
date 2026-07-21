# Phase 10 — Testing and Runtime Validation

## Status

Completed

## Goal

Add GPS tests.

## Prerequisites

Phase 09.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Audit and complete unit, application, contract, PostgreSQL integration, messaging, and
runtime coverage for the full GPS MVP.

## Required Behavior

* Cover domain validation, ingestion/idempotency/late readings, current/history queries,
  tenant isolation, Shipment projections, monitoring rules, outbox retry, and contracts.
* Use PostgreSQL for relational behavior and migration compatibility; avoid replacing
  meaningful integration proof with only EF InMemory.
* Serialize test databases or use isolated names so tests never concurrently drop the same
  database.
* Verify real migration schema and cascade behavior.
* With local infrastructure available, ingest a reading, query current/history, publish an
  outbox event through RabbitMQ, and verify processing state.
* Rebuild and rerun owned Shipment and Notification tests; record unrelated pre-existing
  failures separately and do not modify those services unless GPS caused the regression.
* Inspect full diff, secrets, generated artifacts, and working tree before completion.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj
dotnet ef migrations list --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Full GPS definition of done is satisfied and plan/spec evidence is current.
* Create local commit `test(gps): complete service validation`.

## Work Log

### Completed

Audited all GPS coverage and added a serialized PostgreSQL integration collection backed by
the dedicated `aurora_gps_tracking_tests` database. Tests now prove migration compatibility,
ingestion/query behavior, sequential and concurrent device idempotency, missing/cross-tenant
isolation, Shipment inbox projection, relationship delete behavior, PostgreSQL skip-locked
outbox processing, and actual RabbitMQ delivery. Rebuilt and reran GPS, Shipment, and
Notification regression suites.

### Files Changed

* `src/dotnet/GpsTracking/Tests/Integration/GpsPostgresCollection.cs`
* `src/dotnet/GpsTracking/Tests/Integration/GpsPostgresIntegrationTests.cs`
* `codex/specs/gps-tracking.md`

### Commands Executed

* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --filter FullyQualifiedName~GpsPostgresIntegrationTests --logger console;verbosity=normal`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --logger console;verbosity=minimal`
* `dotnet build src/dotnet/Contracts/GpsTracking.Contracts/GpsTracking.Contracts.csproj --no-restore --verbosity minimal`
* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --no-restore --verbosity minimal`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --no-restore --verbosity minimal`
* `dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --no-restore --logger console;verbosity=minimal`
* `dotnet build src/dotnet/Notification/Notification.csproj --no-restore --verbosity minimal`
* `dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj --no-restore --logger console;verbosity=minimal`
* `dotnet ef migrations list --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj --no-build`
* `docker compose -f docker-compose.dev.yml ps`
* `git diff --check`

### Build Result

Passed: GPS, GPS contracts, Shipment Workflow, and Notification built with 0 errors and
0 warnings.

### Test Result

Passed: GPS 50 tests, Shipment Workflow 99 tests, Notification 29 tests; 0 failed and
0 skipped. Five GPS integration tests use PostgreSQL, including one real RabbitMQ delivery.

### Runtime Result

Passed. Docker PostgreSQL, Redis, and RabbitMQ dependencies were healthy. The real outbox
processor selected a PostgreSQL batch, published `GpsPositionUpdatedEvent` through RabbitMQ,
the temporary consumer received the same EventId/TenantId, and `ProcessedAt` was persisted.
Phase 09 process smoke also confirmed port 5091, Shipment consumer endpoint, signal-loss SQL,
and clean shutdown.

### Migration Result

Both development and test databases report only
`20260721042104_InitialGpsTracking` as applied with no Pending marker. Integration tests run
against the migration-created schema and do not drop either database.

### Remaining Issues

No unresolved GPS MVP issue. Realtime Hub and other downstream GPS event consumers remain
separate service ownership and are intentionally not implemented here.
