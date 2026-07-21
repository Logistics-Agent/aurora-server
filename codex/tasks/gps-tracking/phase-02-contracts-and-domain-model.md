# Phase 02 — Contracts and Domain Model

## Status

Not Started

## Goal

Define GPS contracts and domain.

## Prerequisites

Phase 01.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Define `protos/gps_tracking.proto`, `GpsTracking.Contracts`, and the GPS-owned domain
entities/enums for positions, current snapshots, assignments, geofences, presence state,
alerts, consumed events, and outbox messages.

## Required Behavior

* Declare RPCs for ingestion, current location, bounded history, geofence management,
  and monitoring-alert management. Requests must not expose `TenantId` or a mutable
  client-controlled `ShipmentId`.
* Use `google.protobuf.Timestamp`; use strings for external vehicle/route IDs and UUID
  strings for Aurora aggregate IDs.
* Add versioned GPS event records without EF/runtime dependencies.
* Enforce coordinate, speed, heading, accuracy, timestamp, radius, required-string, and
  lifecycle invariants through constructors/domain methods.
* Keep position history immutable and current snapshots updateable only by newer readings.
* Keep navigation collections/mutation private where aggregate rules apply.
* Preserve explicit external references; create no cross-service entity relationship.

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
* Tests cover every domain boundary and enum/event contract.
* Create local commit `feat(gps): define contracts and domain`.

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
