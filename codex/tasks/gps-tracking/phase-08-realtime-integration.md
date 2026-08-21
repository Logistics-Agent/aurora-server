# Phase 08 — Realtime Integration

## Status

Completed

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

Implemented an allowlisted GPS integration-event registry, MassTransit publisher adapter,
PostgreSQL outbox batch store, processor, validated options, and polling background worker.
The batch store holds a transaction while selecting ordered messages with
`FOR UPDATE SKIP LOCKED`; success marks `ProcessedAt`, failures increment bounded retry
state, and all logs omit payload and coordinates. Event payloads preserve producer-created
IDs and contract fields for idempotent future consumers.

### Files Changed

* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/GpsIntegrationEventPublisher.cs`
* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/GpsIntegrationEventTypeRegistry.cs`
* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/GpsOutboxBatchStore.cs`
* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/GpsOutboxProcessor.cs`
* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/GpsOutboxPublisherBackgroundService.cs`
* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/GpsOutboxPublisherOptions.cs`
* `src/dotnet/GpsTracking/Tests/Infrastructure/GpsOutboxPublisherTests.cs`

### Commands Executed

* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --filter FullyQualifiedName~GpsOutboxPublisherTests`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --logger console;verbosity=minimal`
* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --no-restore --verbosity minimal`
* `git diff --check`

### Build Result

Passed: 4 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 45 tests, 0 failed, 0 skipped. Seven Phase 08 tests cover both allowlisted types,
serialization identity, successful publish, unknown type, publisher failure, retry limit,
and duplicate-safe event IDs.

### Runtime Result

MassTransit and hosted-worker registration plus RabbitMQ smoke validation are deferred to
Phase 09 startup configuration.

### Migration Result

No migration generated. The outbox schema is already in the model and will be created by
the initial GPS migration in Phase 09.

### Remaining Issues

No Phase 08 blocker. PostgreSQL lock behavior and real RabbitMQ publication remain explicit
integration/runtime validation work for Phases 09-10.
