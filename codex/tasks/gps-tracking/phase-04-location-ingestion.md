# Phase 04 — Location Ingestion

## Status

Not Started

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

Not started.

### Files Changed

None.

### Commands Executed

None.

### Build Result

Not started.

### Test Result

Not started.

### Runtime Result

Not started.

### Migration Result

Not started.

### Remaining Issues

Phase has not started.
