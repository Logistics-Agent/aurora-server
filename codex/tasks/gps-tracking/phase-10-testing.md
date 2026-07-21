# Phase 10 — Testing and Runtime Validation

## Status

Not Started

## Goal

Add GPS tests.

## Prerequisites

Phase 09.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Audit and complete unit, application, contract, PostgreSQL integration, messaging, and
runtime coverage for the full GPS MVP.

## Required Behavior

* Cover domain validation, ingestion/idempotency/late readings, current/history queries,
  tenant isolation, Shipment projections, monitoring rules, outbox retry, and contracts.
* Use PostgreSQL for relational behavior and migration compatibility; avoid replacing
  meaningful integration proof with only EF InMemory.
* Serialize test databases or use isolated names so tests never concurrently drop the same
  database.
* Verify real migration schema and cascade behavior.
* With local infrastructure available, ingest a reading, query current/history, publish an
  outbox event through RabbitMQ, and verify processing state.
* Rebuild and rerun owned Shipment and Notification tests; record unrelated pre-existing
  failures separately and do not modify those services unless GPS caused the regression.
* Inspect full diff, secrets, generated artifacts, and working tree before completion.

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
dotnet ef migrations list --project src/dotnet/GpsTracking/GpsTracking.csproj --startup-project src/dotnet/GpsTracking/GpsTracking.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Full GPS definition of done is satisfied and plan/spec evidence is current.
* Create local commit `test(gps): complete service validation`.

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
