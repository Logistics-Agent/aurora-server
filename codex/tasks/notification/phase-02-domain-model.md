# Phase 02 — Domain Model

## Status

Completed

## Goal

Define notification domain model.

## Prerequisites

Phase 01.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

Notification domain entities and enums are implemented.

## Scope

Notification, NotificationDeliveryAttempt, NotificationPreference, templates if needed.

## Required Behavior

Domain supports email and in-app abstractions.

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

* Added NotificationMessage, NotificationPreference, and NotificationDeliveryAttempt.
* Added channel, event, notification, and delivery-attempt enums.
* Enforced tenant, user, event, recipient, text, attempt, and transition invariants.

### Files Changed

* src/dotnet/Notification/Domain/Entities
* src/dotnet/Notification/Domain/Enums
* tests/dotnet/Notification.Tests/NotificationDomainTests.cs

### Commands Executed

```bash
dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj --filter NotificationDomainTests
dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj
dotnet build src/dotnet/Notification/Notification.csproj
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 6 tests, 0 warnings.

### Runtime Result

Not run; runtime configuration is Phase 07 scope.

### Migration Result

Not run; persistence and migrations are later phases.

### Remaining Issues

Persistence mapping remains Phase 03 scope.

### Commit Hash

Recorded by the Phase 02 Git commit.
