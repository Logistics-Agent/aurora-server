# Phase 06 — Extraction Pipeline

## Status

Not Started

## Goal

Implement extraction pipeline.

## Prerequisites

Phase 05.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement the application pipeline that claims a queued job, obtains approved content, invokes
the configured OCR provider, normalizes extracted fields, calculates confidence/review state,
persists the result, and creates an outbox event in one transaction.

## Required Behavior

* Resolve TenantId from current-user context for API submissions and use explicit trusted TenantId
  for background processing; never infer a tenant from client JSON.
* Make submission idempotent by `(TenantId, IdempotencyKey)` and return the existing job on replay.
* Validate storage reference, file metadata, supported type/size, and external IDs before enqueueing.
* Transition Queued to Processing through domain methods and prevent concurrent double processing.
* Normalize provider fields to a documented JSON schema with stable field names and optional raw-text
  references; malformed provider output must not be marked completed.
* Calculate overall confidence in `[0,1]` and set `NeedsReview` using a configured threshold plus
  required-field rules; confidence is extraction quality, not compliance confidence.
* Persist completion plus `DocumentOcrCompletedEvent` outbox atomically; record classified failure
  for Phase 07 retry handling and never write directly to Shipment Workflow.
* Implement the Phase 02 gRPC submit/get/list mappings without adding callbacks.

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
* Tests cover idempotent submission, normalization, confidence/review rules, tenant isolation,
  atomic result/outbox writes, provider failures, and API mapping.
* Create local commit `feat(ocr): implement extraction pipeline`.

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
