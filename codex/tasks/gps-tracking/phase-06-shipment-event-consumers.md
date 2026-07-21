# Phase 06 — Shipment Event Consumers

## Status

Completed

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

Implemented MassTransit consumers and an idempotent local projector for RouteAssigned,
ShipmentCancelled, and ShipmentCompleted events. Assignment state, terminal shipment
state, and inbox receipt save atomically with explicit tenant predicates. Terminal events
prevent later out-of-order assignments from reopening tracking.

### Files Changed

* `src/dotnet/GpsTracking/Application/Consumers/ShipmentTrackingConsumer.cs`
* `src/dotnet/GpsTracking/Application/Shipments/ShipmentAssignmentProjector.cs`
* `src/dotnet/GpsTracking/Domain/Entities/ShipmentTrackingState.cs`
* `src/dotnet/GpsTracking/Infrastructure/Persistences/GpsTrackingDbContext.cs`
* `src/dotnet/GpsTracking/Tests/Application/ShipmentAssignmentProjectorTests.cs`
* `src/dotnet/GpsTracking/Tests/GpsPersistenceModelTests.cs`

### Commands Executed

* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore` (expected RED: missing projector)
* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --no-restore --verbosity minimal`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --logger console;verbosity=minimal`

### Build Result

Passed: 4 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 31 tests, 0 failed, 0 skipped, including business-duplicate regression coverage.

### Runtime Result

Consumer runtime wiring is deferred to Phase 09; consumer/projector behavior is tested directly.

### Migration Result

No migration generated; the new local projection table is included in the Phase 09 model.

### Remaining Issues

No Phase 06 issues. GPS uses Shipment contracts only and never accesses Shipment storage.
