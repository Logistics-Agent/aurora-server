# Phase 03 — Persistence

## Status

Completed

## Goal

Configure Notification persistence.

## Prerequisites

Phase 02.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

Notification persistence model is implemented and migration-ready.

## Scope

DbContext, DbSets, tenant filters, indexes, outbox/inbox/idempotency where needed.

## Required Behavior

Database model is tenant-aware and migration-ready.

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

* Added NotificationDbContext with tenant filters for every tenant-owned entity.
* Added inbox receipt persistence, aggregate cascade mapping, and dedupe/query indexes.
* Aligned EF Core test dependencies to 10.0.9.

### Files Changed

* src/dotnet/Notification/Domain/Entities/ConsumedIntegrationEvent.cs
* src/dotnet/Notification/Infrastructure/Persistences/NotificationDbContext.cs
* src/dotnet/Notification/Notification.csproj
* src/dotnet/Notification/Tests/Notification.Tests.csproj
* src/dotnet/Notification/Tests/NotificationPersistenceModelTests.cs

### Commands Executed

```bash
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
dotnet build src/dotnet/Notification/Notification.csproj
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 9 tests, 0 warnings.

### Runtime Result

Not run; runtime configuration is Phase 07 scope.

### Migration Result

Model is migration-ready; migration generation is Phase 08 scope.

### Remaining Issues

Shipment event consumers remain Phase 04 scope.

### Commit Hash

Recorded by the Phase 03 Git commit.
