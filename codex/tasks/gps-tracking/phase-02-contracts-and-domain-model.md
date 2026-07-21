# Phase 02 — Contracts and Domain Model

## Status

Completed

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

Defined the complete tenant-safe GPS gRPC surface, standalone versioned integration-event
contracts, service-owned enums, and domain entities with validation and controlled
mutation. The ingestion request intentionally contains neither TenantId nor ShipmentId.

### Files Changed

* `protos/gps_tracking.proto`
* `src/dotnet/Contracts/GpsTracking.Contracts/`
* `src/dotnet/GpsTracking/GpsTracking.csproj`
* `src/dotnet/GpsTracking/Domain/Entities/`
* `src/dotnet/GpsTracking/Domain/Enums/`
* `src/dotnet/GpsTracking/Tests/GpsDomainTests.cs`
* `src/dotnet/GpsTracking/Tests/GpsProtoContractTests.cs`

### Commands Executed

* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore` (expected RED: missing Phase 02 types)
* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --verbosity minimal`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --logger console;verbosity=minimal`

### Build Result

Passed: 4 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 9 tests, 0 failed, 0 skipped.

### Runtime Result

Not required; no business endpoint implementation is mapped in this phase.

### Migration Result

Not required; EF mapping begins in Phase 03.

### Remaining Issues

No Phase 02 issues. Persistence, handlers, consumers, monitoring, and publication remain
in their designated later phases.
