# Phase 09 — Program and Migration

## Status

Not Started

## Goal

Configure startup and migration.

## Prerequisites

Phase 08.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Wire production startup, add local Docker PostgreSQL infrastructure, generate/review the
initial GPS migration, apply it only to the confirmed GPS database, and smoke-start the
service.

## Required Behavior

* Register gRPC interceptors, shared services, DbContext, application services, Shipment
  consumers, MassTransit, monitoring worker, outbox publisher, options, and TimeProvider.
* Configure secrets through environment/connection-string providers; commit placeholders
  only.
* Add a dedicated `aurora_gps_tracking` PostgreSQL container/database and non-conflicting
  local port.
* Generate exactly one initial migration and review tables, precision, filters, indexes,
  constraints, and cascade behavior.
* Confirm the target connection before applying; never reset/drop another service DB.
* Run migration list before/after update and inspect PostgreSQL tables/indexes.
* Start the service with available Docker dependencies and record actual logs/result.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet ef migrations add InitialGpsTracking --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj --output-dir Infrastructure/Persistences/Migrations
dotnet ef migrations list --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj
dotnet ef database update --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Migration and runtime results are recorded truthfully.
* Create local commit `feat(gps): configure startup and migration`.

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
