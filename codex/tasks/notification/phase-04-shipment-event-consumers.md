# Phase 04 — Shipment Event Consumers

## Status

Completed

## Goal

Consume Shipment events.

## Prerequisites

Phase 03.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

ShipmentCreated, ShipmentStatusChanged, and ShipmentCancelled consumers are implemented with tenant-scoped projection and inbox deduplication.

## Scope

ShipmentCreated/StatusChanged/Cancelled consumers.

## Required Behavior

Consumers are idempotent and do not query Shipment DB.

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

* Added MassTransit consumers for ShipmentCreatedEvent, ShipmentStatusChangedEvent, and ShipmentCancelledEvent.
* Added contract-to-notification mapping without querying Shipment Workflow data.
* Added tenant-scoped preference projection and atomic inbox receipt persistence.
* Added duplicate-event, cross-tenant preference, disabled-preference, and event-mapping tests.

### Files Changed

* src/dotnet/Notification/Application/Consumers/ShipmentNotificationConsumer.cs
* src/dotnet/Notification/Application/Services/ShipmentEventNotificationFactory.cs
* src/dotnet/Notification/Application/Services/ShipmentNotificationProjector.cs
* tests/dotnet/Notification.Tests/Application/ShipmentEventNotificationFactoryTests.cs
* tests/dotnet/Notification.Tests/Application/ShipmentNotificationProjectorTests.cs
* tests/dotnet/Notification.Tests/Notification.Tests.csproj
* codex/tasks/notification/phase-04-shipment-event-consumers.md
* codex/plan.md

### Commands Executed

```bash
git status --short
git branch --show-current
git log --oneline --decorate -6
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj
git diff --check
```

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 15 tests, 0 warnings.

### Runtime Result

Not run; broker and dependency registration are Phase 07 scope.

### Migration Result

No migration generated; migration creation and application are Phase 08 scope.

### Remaining Issues

* Live delivery depends on Shipment outbox publication by the integration owner.
* Notification delivery providers and background processing remain Phase 05 scope.

### Commit Hash

Recorded by the Phase 04 Git commit.
