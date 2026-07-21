# Phase 07 — Monitoring Rules

## Status

Not Started

## Goal

Implement monitoring rules.

## Prerequisites

Phase 06.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement circular geofence APIs/state, ingestion-time abnormal-stop/geofence evaluation,
and a bounded signal-loss background scan.

## Required Behavior

* Geofences validate name, centre, radius (0-100 km), optional vehicle/shipment scope,
  active state, and tenant ownership.
* Use a deterministic Haversine calculation and persisted presence state to emit entry or
  exit only when state changes.
* Track stationary time from current snapshots; raise abnormal-stop only after configured
  low-speed duration.
* Raise signal-loss only for active assignments whose latest reading exceeds the configured
  threshold.
* Deduplicate active alerts and allow tenant users to list and resolve them.
* Persist alert plus `GpsMonitoringAlertRaisedEvent` outbox message atomically.
* Keep thresholds/options validated on startup and scans bounded.

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
* Tests cover geofence transitions, distance boundaries, abnormal-stop timing, signal loss,
  deduplication, resolution, and tenant filtering.
* Create local commit `feat(gps): add monitoring rules`.

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
