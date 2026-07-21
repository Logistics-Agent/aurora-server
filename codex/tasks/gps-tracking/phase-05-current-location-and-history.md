# Phase 05 — Current Location and History

## Status

Not Started

## Goal

Implement query APIs.

## Prerequisites

Phase 04.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement `GetCurrentLocation` and `ListPositionHistory` application/query and gRPC paths.

## Required Behavior

* Require exactly one vehicle or shipment selector and current tenant context.
* Return NotFound without revealing another tenant's data.
* Use no-tracking queries and indexed predicates.
* Validate UTC range ordering, maximum seven-day window, page >= 1, and page size 1-500.
* Order history deterministically by `RecordedAt DESC, Id DESC` and return total/page
  metadata.
* Return current location only from the snapshot table; do not scan all history.
* Do not expose route geometry, ETA, or detailed Shipment data.

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
* Tests cover selectors, paging/range bounds, stable ordering, not-found semantics, and
  cross-tenant isolation.
* Create local commit `feat(gps): implement location queries`.

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
