# Phase 04 — Location Ingestion

## Status

Completed

## Goal

Implement GPS ingestion.

## Prerequisites

Phase 03.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement the complete unary `IngestPosition` path from gRPC validation through atomic
history/current-snapshot/outbox persistence.

## Required Behavior

* Require authenticated tenant context and validate device ID, vehicle ID, external
  reading ID, coordinates, motion values, and recorded timestamp.
* Resolve ShipmentId only from the tenant's active vehicle assignment.
* Deduplicate retries by `(TenantId, DeviceId, ExternalReadingId)` and return the original
  accepted result without duplicate history or outbox rows.
* Store accepted late readings in history but advance current location only for a newer
  `(RecordedAt, Id)` value.
* Save position, snapshot, and `GpsPositionUpdatedEvent` outbox row in one transaction.
* Do not publish directly and do not evaluate Phase 07 monitoring rules yet.
* Translate domain/input failures to stable gRPC status codes without stack traces.

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
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Tests cover validation, missing tenant, assignment derivation, idempotency, late data,
  atomic outbox creation, and tenant isolation.
* Create local commit `feat(gps): implement position ingestion`.

## Work Log

### Completed

Implemented tenant-authenticated position ingestion, active-assignment ShipmentId
derivation, immutable history, monotonic current snapshots, device-reading idempotency,
concurrent unique-conflict recovery, and atomic position/snapshot/outbox persistence.
Added the gRPC ingestion method with stable unauthenticated/invalid-argument responses.

### Files Changed

* `src/dotnet/GpsTracking/Application/Ingestion/PositionIngestionService.cs`
* `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs`
* `src/dotnet/GpsTracking/Tests/Application/PositionIngestionServiceTests.cs`
* `src/dotnet/GpsTracking/Tests/Grpc/GpsTrackingGrpcServiceTests.cs`
* `src/dotnet/GpsTracking/Tests/Grpc/TestServerCallContext.cs`

### Commands Executed

* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore` (expected RED: missing ingestion service)
* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --no-restore --verbosity minimal`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --logger console;verbosity=minimal`

### Build Result

Passed: 4 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 19 tests, 0 failed, 0 skipped, 0 warnings.

### Runtime Result

Not started as a process; the gRPC service method is covered directly and startup wiring remains Phase 09.

### Migration Result

No migration generated; schema migration remains Phase 09.

### Remaining Issues

No Phase 04 issues. Monitoring evaluation remains intentionally deferred to Phase 07 and
outbox publication to Phase 08.
