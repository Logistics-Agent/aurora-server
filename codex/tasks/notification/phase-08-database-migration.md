# Phase 08 — Database Migration

## Status

Completed

## Goal

Create/apply Notification migration.

## Prerequisites

Phase 07.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

The initial Notification migration is generated, applied, and verified against the service-owned local PostgreSQL database.

## Scope

Generate and validate migration for Notification DB.

## Required Behavior

Migration applies to Notification DB only.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/Notification/Notification.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.

## Work Log

### Completed

* Added an isolated PostgreSQL 16 development container for aurora_notification on host port 5434.
* Generated the InitialNotification EF Core migration and reviewed tables, nullability, indexes, unique constraints, and cascade behavior.
* Confirmed the target database identity before applying the migration.
* Applied and re-listed the migration, then verified tables, indexes, and migration history through PostgreSQL.
* Re-ran runtime smoke validation with successful database polling and RabbitMQ connection.

### Files Changed

* docker-compose.dev.yml
* src/dotnet/Notification/Infrastructure/Persistences/Migrations/20260719124939_InitialNotification.cs
* src/dotnet/Notification/Infrastructure/Persistences/Migrations/20260719124939_InitialNotification.Designer.cs
* src/dotnet/Notification/Infrastructure/Persistences/Migrations/NotificationDbContextModelSnapshot.cs
* codex/tasks/notification/phase-08-database-migration.md
* codex/plan.md

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
docker compose -f docker-compose.dev.yml config
docker compose -f docker-compose.dev.yml up -d notification-postgres
docker exec aurora-notification-postgres psql -U postgres -d aurora_notification -c "SELECT current_database(), current_user;"
dotnet ef migrations add InitialNotification --project src/dotnet/Notification/Notification.csproj --startup-project src/dotnet/Notification/Notification.csproj --output-dir Infrastructure/Persistences/Migrations
dotnet ef migrations list --project src/dotnet/Notification/Notification.csproj --startup-project src/dotnet/Notification/Notification.csproj
dotnet ef database update --project src/dotnet/Notification/Notification.csproj --startup-project src/dotnet/Notification/Notification.csproj
timeout 10s dotnet run --project src/dotnet/Notification/Notification.csproj --no-build
git diff --check
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 22 tests, 0 warnings.

### Runtime Result

Passed smoke validation: host listened on http://localhost:5090, RabbitMQ bus started, and the delivery worker queried Notification PostgreSQL successfully. The bounded command ended by intentional timeout (exit 124).

### Migration Result

Applied 20260719124939_InitialNotification to confirmed database aurora_notification. Verified __EFMigrationsHistory plus notifications, notification_preferences, notification_delivery_attempts, and consumed_integration_events tables and their indexes.

### Remaining Issues

* Full PostgreSQL-backed behavior and gRPC coverage remain Phase 09 scope.
* SMTP credentials remain deployment configuration and were not committed.

### Commit Hash

Recorded by the Phase 08 Git commit.
