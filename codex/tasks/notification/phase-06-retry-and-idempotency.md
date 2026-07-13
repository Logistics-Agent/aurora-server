# Phase 06 — Retry and Idempotency

## Status

Not Started

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

Production implementation has not started for this service.

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
# Replace with the service project path once created.
dotnet build
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.

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
