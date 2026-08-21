# Phase 07 — Retry and Job Processing

## Status

Not Started

## Goal

Implement retry/job worker.

## Prerequisites

Phase 06.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement bounded background job claiming/recovery, retry scheduling, permanent-failure handling,
and transactional outbox publication for OCR completion/failure events.

## Required Behavior

* Claim ordered batches with PostgreSQL row locking (`FOR UPDATE SKIP LOCKED`) or the proven
  repository equivalent so multiple workers cannot process the same job concurrently.
* Store processing lease/heartbeat data and safely recover jobs abandoned after process failure.
* Retry only classified transient failures with configured maximum attempts and deterministic
  exponential backoff plus bounded jitter; cancellation and permanent errors are not retried.
* Persist attempt history, next-attempt time, final bounded error, and terminal status.
* On terminal failure, atomically add `DocumentOcrFailedEvent`; completion/failure events publish
  only through an allowlisted outbox publisher with preserved EventId and bounded retries.
* Worker cancellation must stop cleanly without marking an incomplete provider call successful.
* Logs and metrics include job/provider/status/attempt identifiers but not document contents,
  extracted PII, provider credentials, or full storage references.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Tests cover concurrent claim, lease recovery, retry schedule/limit, permanent failure,
  cancellation, outbox allowlist, and duplicate-safe event IDs.
* Create local commit `feat(ocr): process document jobs`.

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
