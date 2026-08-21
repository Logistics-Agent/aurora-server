# Phase 03 — Document Job Model

## Status

Not Started

## Goal

Model OCR jobs.

## Prerequisites

Phase 02.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement the OCR aggregate and supporting entities/enums needed to represent one idempotent
document extraction job, its attempts, normalized result, and integration-event intent.

## Required Behavior

* Model `DocumentOcrJob` with tenant ownership, idempotency key, storage reference, file metadata,
  external document/shipment IDs, type hint/detected type, status, attempts, timestamps, and error.
* Model provider attempts separately with provider name, started/completed time, outcome, and
  bounded diagnostics; never persist credentials or unrestricted raw provider payloads.
* Model normalized JSON, overall confidence `[0,1]`, per-field confidence when required, and
  `NeedsReview` without deciding regulatory compliance.
* Define explicit states such as Queued, Processing, Completed, Failed, and Cancelled, preserving
  retryable versus terminal failure semantics.
* Add domain methods for start, complete, record failure, schedule retry, and cancel; reject
  invalid transitions and unrestricted public mutation.
* Include inbox/outbox domain records only when needed by the approved contracts; no EF mapping yet.

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
* Tests cover state transitions, confidence bounds, required metadata, error limits, and tenant IDs.
* Create local commit `feat(ocr): model document jobs`.

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
