# Phase 09 — Program and Migration

## Status

Completed

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

Configured the GPS gRPC host with shared authentication/exception interceptors, tenant
context, PostgreSQL, Shipment event consumers, MassTransit/RabbitMQ, application services,
monitoring and outbox workers, validated options, and TimeProvider. Added a dedicated local
GPS PostgreSQL service on port 5435. Generated, reviewed, and applied the single
`20260721042104_InitialGpsTracking` migration to the confirmed `aurora_gps_tracking`
database, then smoke-started the complete process.

### Files Changed

* `docker-compose.dev.yml`
* `src/dotnet/GpsTracking/Program.cs`
* `src/dotnet/GpsTracking/appsettings.json`
* `src/dotnet/GpsTracking/appsettings.Development.json`
* `src/dotnet/GpsTracking/Infrastructure/Persistences/Migrations/20260721042104_InitialGpsTracking.cs`
* `src/dotnet/GpsTracking/Infrastructure/Persistences/Migrations/20260721042104_InitialGpsTracking.Designer.cs`
* `src/dotnet/GpsTracking/Infrastructure/Persistences/Migrations/GpsTrackingDbContextModelSnapshot.cs`

### Commands Executed

* `docker compose -f docker-compose.dev.yml config --quiet`
* `docker compose -f docker-compose.dev.yml up -d gps-postgres redis rabbitmq`
* `docker exec aurora-gps-postgres psql -U postgres -d aurora_gps_tracking -c 'SELECT current_database(), current_user;'`
* `dotnet ef migrations list --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj --no-build`
* `dotnet ef migrations add InitialGpsTracking --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj --output-dir Infrastructure/Persistences/Migrations --no-build`
* `dotnet ef database update --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj --no-build`
* `docker exec aurora-gps-postgres psql ...` table and index inspection queries
* `dotnet run --project src/dotnet/GpsTracking/GpsTracking.csproj --no-build --launch-profile http`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --logger console;verbosity=minimal`
* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --no-restore --verbosity minimal`
* `git diff --check`

### Build Result

Passed: 4 projects built with 0 errors and 0 warnings after migration generation.

### Test Result

Passed: 45 tests, 0 failed, 0 skipped.

### Runtime Result

Passed. GPS listened on `http://localhost:5091`; MassTransit configured
`ShipmentTrackingConsumer` and connected to RabbitMQ. Signal-loss SQL and outbox
`FOR UPDATE SKIP LOCKED` SQL executed successfully. The process shut down cleanly with
RabbitMQ bus stopped after the smoke interval.

### Migration Result

Applied `20260721042104_InitialGpsTracking` to the confirmed local
`aurora_gps_tracking` database. The migration list has no Pending marker. PostgreSQL
inspection found migration history plus all nine GPS-owned tables and expected indexes.

### Remaining Issues

No Phase 09 blocker. Local development credentials are isolated to development config;
production connection values remain environment/provider supplied. PostgreSQL-backed
behavioral integration tests and broker publication proof remain Phase 10 scope.
