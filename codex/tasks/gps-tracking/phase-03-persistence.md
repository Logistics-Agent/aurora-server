# Phase 03 — Persistence

## Status

Not Started

## Goal

Configure GPS persistence.

## Prerequisites

Phase 02.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement `GpsTrackingDbContext`, entity mappings, relationships, tenant query filters,
and model-level tests. Do not generate a migration in this phase.

## Required Behavior

* Map separate tables for positions, current locations, assignments, geofences,
  geofence presence, alerts, consumed events, and outbox messages.
* Add unique indexes for reading idempotency, one current snapshot per vehicle, active
  assignment lookup, consumed event identity, geofence presence, and outbox event IDs.
* Add tenant/time indexes supporting vehicle/shipment history, active alerts, signal-loss
  scans, and outbox batches.
* Apply tenant filters to every tenant-owned table. A missing tenant must match no rows.
* Configure only GPS-owned relationships and conservative cascade behavior.
* Use decimal precision suitable for coordinates and speed; keep event content as text.

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
* EF model tests verify filters, keys, indexes, precision, and delete behavior.
* Create local commit `feat(gps): configure persistence`.

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
