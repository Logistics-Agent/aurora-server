# Phase 06 — Retry and Idempotency

## Status

Completed

## Goal

Add retry/idempotency behavior.

## Prerequisites

Phase 05.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

Bounded retry scheduling, delivery failure records, and event/notification dedupe constraints are implemented.

## Scope

Retry policy, dedupe keys, failure records.

## Required Behavior

Duplicate events do not duplicate user-visible notifications.

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

* Added configurable bounded exponential backoff with a maximum attempt count.
* Persisted NextAttemptAt and indexed retry eligibility.
* Prevented provider calls before a retry is due and after permanent or exhausted failures.
* Preserved event inbox and user-visible notification unique dedupe keys.
* Normalized provider exceptions into recorded transient failures.
* Added retry delay, successful retry, permanent failure, and exhausted retry tests.

### Files Changed

* src/dotnet/Notification/Application/Delivery/NotificationRetryPolicy.cs
* src/dotnet/Notification/Application/Delivery/NotificationDeliveryService.cs
* src/dotnet/Notification/Domain/Entities/NotificationMessage.cs
* src/dotnet/Notification/Infrastructure/Persistences/NotificationDbContext.cs
* tests/dotnet/Notification.Tests/Application/NotificationDeliveryServiceTests.cs
* tests/dotnet/Notification.Tests/Application/NotificationRetryTests.cs
* codex/tasks/notification/phase-06-retry-and-idempotency.md
* codex/plan.md

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj
git diff --check
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 22 tests, 0 warnings.

### Runtime Result

Not run; hosted processing and dependency registration are Phase 07 scope.

### Migration Result

NextAttemptAt and its retry index are migration-ready; migration generation remains Phase 08 scope.

### Remaining Issues

* Hosted polling and runtime registrations remain Phase 07 scope.
* Concurrent duplicate inserts are guarded by database unique indexes; PostgreSQL integration coverage remains Phase 09 scope.

### Commit Hash

Recorded by the Phase 06 Git commit.
