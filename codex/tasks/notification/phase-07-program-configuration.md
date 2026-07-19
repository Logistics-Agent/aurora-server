# Phase 07 — Program Configuration

## Status

Completed

## Goal

Configure startup.

## Prerequisites

Phase 06.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

Notification startup now registers gRPC, PostgreSQL, shared auth services, MassTransit consumers, delivery providers, retry policy, and the delivery worker.

## Scope

gRPC/worker registrations, MassTransit, DbContext, shared services.

## Required Behavior

Service starts with local dependencies.

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

* Configured gRPC with shared auth and exception interceptors.
* Registered NotificationDbContext against the service-owned PostgreSQL connection.
* Registered the Shipment event consumer with MassTransit/RabbitMQ.
* Registered provider-neutral delivery, retry policy, SMTP/in-app providers, and the hosted delivery worker.
* Implemented all Notification gRPC methods with tenant- and user-scoped queries and mutation.
* Added safe local RabbitMQ credentials and secret-free SMTP/worker/retry configuration.
* Verified host startup, gRPC port binding, and RabbitMQ bus connection.

### Files Changed

* src/dotnet/Notification/Program.cs
* src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs
* src/dotnet/Notification/Infrastructure/BackgroundJobs/NotificationDeliveryWorker.cs
* src/dotnet/Notification/Infrastructure/Providers/SmtpEmailNotificationProvider.cs
* src/dotnet/Notification/appsettings.json
* src/dotnet/Notification/appsettings.Development.json
* codex/tasks/notification/phase-07-program-configuration.md
* codex/plan.md

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj
docker compose -f docker-compose.dev.yml ps
timeout 15s dotnet run --project src/dotnet/Notification/Notification.csproj --no-build
git diff --check
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 22 tests, 0 warnings.

### Runtime Result

Host started on http://localhost:5090 and RabbitMQ bus started. The 15-second smoke command ended by intentional timeout (exit 124). Delivery polling logged connection refused for the not-yet-created Notification PostgreSQL endpoint at localhost:5434; this is resolved by Phase 08 Docker and migration scope.

### Migration Result

No migration generated or applied; Phase 08 owns the initial Notification migration.

### Remaining Issues

* Notification PostgreSQL container and schema remain Phase 08 scope.
* SMTP credentials are intentionally absent; deployment integration must provide them through configuration.

### Commit Hash

Recorded by the Phase 07 Git commit.
