# Phase 05 — Notification Delivery

## Status

Completed

## Goal

Implement delivery abstractions.

## Prerequisites

Phase 04.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

Email and in-app delivery abstractions are implemented with recorded delivery attempts and deterministic test providers.

## Scope

Email and in-app provider interfaces, fake provider for tests.

## Required Behavior

Delivery attempts are recorded.

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

* Added channel-neutral delivery request and result contracts.
* Added explicit email and in-app provider interfaces.
* Added the in-app provider with deterministic local delivery IDs.
* Added the delivery service with persisted in-progress, success, transient-failure, and permanent-failure attempts.
* Added deterministic fake-email tests for success, failure, and duplicate-delivery prevention.

### Files Changed

* src/dotnet/Notification/Application/Delivery/NotificationDeliveryContracts.cs
* src/dotnet/Notification/Application/Delivery/InAppNotificationProvider.cs
* src/dotnet/Notification/Application/Delivery/NotificationDeliveryService.cs
* tests/dotnet/Notification.Tests/Application/NotificationDeliveryServiceTests.cs
* codex/tasks/notification/phase-05-notification-delivery.md
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

Passed: 19 tests, 0 warnings.

### Runtime Result

Not run; dependency registration and hosted processing are Phase 07 scope.

### Migration Result

No migration generated; migration creation and application are Phase 08 scope.

### Remaining Issues

* Bounded retry scheduling and stale in-progress recovery remain Phase 06 scope.
* A production email adapter requires provider configuration from the integration owner; tests use a deterministic fake.

### Commit Hash

Recorded by the Phase 05 Git commit.
