# Phase 09 — Testing

## Status

Completed

## Goal

Add Notification tests.

## Prerequisites

Phase 08.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

Notification domain, consumer, delivery, retry, gRPC, tenant isolation, and PostgreSQL integration behavior are covered by the completed test suite.

## Scope

Domain, consumer, delivery, retry, idempotency tests.

## Required Behavior

Relevant tests pass with fake providers.

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

* Preserved all existing domain, mapping, delivery, retry, idempotency, and persistence-model tests.
* Added gRPC coverage for missing identity, tenant/recipient isolation, mark-read behavior, unread pagination, and preference upsert.
* Added a PostgreSQL-backed integration test for event projection, inbox dedupe, in-app delivery, delivery-attempt persistence, and cross-tenant filtering.
* Used deterministic fake/in-app providers; no paid provider credential is required by tests.
* Verified migration state, healthy Docker dependencies, clean test-data cleanup, and runtime startup.

### Files Changed

* tests/dotnet/Notification.Tests/Grpc/TestServerCallContext.cs
* tests/dotnet/Notification.Tests/Grpc/NotificationGrpcServiceTests.cs
* tests/dotnet/Notification.Tests/Integration/NotificationPostgresIntegrationTests.cs
* codex/tasks/notification/phase-09-testing.md
* codex/requirement.md
* codex/specs/notification.md
* codex/plan.md

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj
dotnet ef migrations list --project src/dotnet/Notification/Notification.csproj --startup-project src/dotnet/Notification/Notification.csproj
docker compose -f docker-compose.dev.yml ps
docker exec aurora-notification-postgres psql -U postgres -d aurora_notification -c "SELECT notifications AS table_name, count(*) FROM notifications UNION ALL SELECT preferences, count(*) FROM notification_preferences UNION ALL SELECT attempts, count(*) FROM notification_delivery_attempts UNION ALL SELECT consumed_events, count(*) FROM consumed_integration_events;"
timeout 10s dotnet run --project src/dotnet/Notification/Notification.csproj --no-build
git diff --check
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 29 tests, 0 warnings. Includes PostgreSQL-backed integration coverage against the applied local Notification migration.

### Runtime Result

Passed smoke validation: host listened on http://localhost:5090, ShipmentNotification consumer and RabbitMQ bus started, and the delivery worker queried PostgreSQL successfully. The bounded command ended by intentional timeout (exit 124).

### Migration Result

20260719124939_InitialNotification remains applied to aurora_notification. Migration list succeeded and the integration test cleaned all tenant-scoped test records.

### Remaining Issues

* Live Shipment-to-Notification delivery still requires the Shipment outbox publisher integration owned outside this service.
* SMTP host, sender, and credentials must be supplied by deployment configuration for real email delivery.

### Commit Hash

Recorded by the Phase 09 Git commit.
