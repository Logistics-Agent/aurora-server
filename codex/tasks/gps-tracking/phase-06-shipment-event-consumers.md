# Phase 06 — Shipment Event Consumers

## Status

Not Started

## Goal

Consume relevant Shipment events.

## Prerequisites

Phase 05.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Consume `RouteAssignedEvent`, `ShipmentCancelledEvent`, and `ShipmentCompletedEvent` to
maintain local vehicle-shipment assignment references.

## Required Behavior

* Validate non-empty trusted event IDs, TenantId, ShipmentId, and required assignment data.
* Create or replace an active assignment on route assignment; close matching assignments
  on cancellation/completion without deleting GPS history.
* Record one inbox receipt in the same transaction as the projection.
* Deduplicate by `(SourceEventType, SourceEventId)` across broker redelivery.
* Query with explicit TenantId under background consumers; never rely on missing request
  context and never query Shipment Workflow.
* Keep out-of-order terminal events safe and idempotent.

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
* Tests cover duplicate, cross-tenant, reassignment, cancellation, completion, and
  out-of-order behavior.
* Create local commit `feat(gps): consume shipment events`.

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
