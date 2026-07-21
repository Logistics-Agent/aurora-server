# Phase 09 — Testing

## Status

Not Started

## Goal

Add OCR tests.

## Prerequisites

Phase 08.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Audit and complete domain, application, contract, PostgreSQL, messaging, security, and runtime
coverage for the full Document OCR MVP, then verify owned-service regressions.

## Required Behavior

* Cover domain transitions, validation, confidence/review rules, normalization, provider error
  classification, retries, leases, cancellation, idempotency, and tenant isolation.
* Add PostgreSQL-backed tests for migration schema, unique keys, restrictive missing-tenant filter,
  concurrent job claim, JSON persistence, relationships, and outbox locking.
* Test Submit/Get/List gRPC mappings, no client TenantId, cross-tenant NotFound behavior, document
  limits, unsupported formats, and unsafe storage reference rejection.
* Test deterministic fake extraction end-to-end from submission through completed result/outbox.
* With RabbitMQ available, prove one completion event and one permanent-failure event are delivered
  with matching EventId/TenantId and marked processed.
* Smoke-start the service with Docker dependencies; tests must not need paid provider credentials.
* Rebuild/rerun Shipment, Notification, and GPS regressions and document unrelated failures without
  modifying those services unless OCR caused the regression.
* Inspect migration state, complete diff, secrets, generated artifacts, and working tree.

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
dotnet ef migrations list --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Full OCR definition of done passes with actual test count, migration, runtime, and broker evidence.
* Create local commit `test(ocr): complete service validation`.

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
