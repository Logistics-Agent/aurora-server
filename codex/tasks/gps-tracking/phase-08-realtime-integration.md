# Phase 08 — Realtime Integration

## Status

Not Started

## Goal

Publish realtime tracking updates.

## Prerequisites

Phase 07.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Complete GPS integration-event contracts and implement the transactional outbox publisher
used by Realtime Hub and other future consumers.

## Required Behavior

* Publish only allowlisted `GpsPositionUpdatedEvent` and
  `GpsMonitoringAlertRaisedEvent` types through MassTransit.
* Select ordered bounded batches with PostgreSQL `FOR UPDATE SKIP LOCKED`.
* Record `ProcessedAt`, bounded retry count, and bounded error text.
* Do not retry messages at or above the configured maximum.
* Preserve event IDs, TenantId, recorded/occurred times, external references, and contract
  version during serialization.
* Do not implement or call Realtime Hub, Notification, or another consumer.
* Add structured logs around batch success/failure without coordinates or user secrets.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Tests cover registry, serialization, successful publish, unknown type, retry limit, and
  duplicate-safe event IDs.
* Create local commit `feat(gps): publish tracking events`.

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
