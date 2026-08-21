# Phase 04 — Persistence

## Status

Completed

## Goal

Configure OCR persistence.

## Prerequisites

Phase 03.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Add `DocumentOcrDbContext`, entity configurations, tenant filters, aggregate relationships,
inbox/outbox persistence, and indexes required by submission, polling, and worker queries.

## Required Behavior

* Configure separate tables for jobs, provider attempts/results where modeled, inbox receipts,
  and outbox messages; external Shipment/document references have no database foreign keys.
* Apply tenant query filters to every tenant-owned entity; missing tenant context must return no
  tenant rows rather than disabling filters.
* Add unique `(TenantId, IdempotencyKey)` and appropriate external-reference indexes.
* Add worker indexes for `(Status, NextAttemptAt, CreatedAt)` and outbox indexes for
  `(ProcessedAt, RetryCount, OccurredAt)`.
* Configure bounded string lengths, JSON column storage, confidence precision, required timestamps,
  cascade behavior only inside the OCR aggregate, and unique EventId/source-event constraints.
* Add EF model tests for filters, indexes, conversions, and relationships.
* Do not create a migration in this phase; Phase 08 owns the initial migration.

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
* Persistence model tests prove tenant filters remain restrictive with missing context.
* Create local commit `feat(ocr): configure persistence`.

## Work Log

### Completed

Implemented the OCR DbContext with job, provider-attempt, inbox, and outbox mappings. Every entity
has a restrictive tenant filter; attempts use a composite tenant/job foreign key with aggregate
cascade; JSON, confidence, lengths, enum conversions, and worker/outbox/idempotency indexes are
configured without creating a migration.

### Files Changed

* `src/dotnet/DocumentOcr/DocumentOcr.csproj`
* `src/dotnet/DocumentOcr/Infrastructure/Persistences/DocumentOcrDbContext.cs`
* `src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj`
* `src/dotnet/DocumentOcr/Tests/DocumentOcrPersistenceModelTests.cs`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 20 tests, 0 failed, 0 warnings. The first model test run exposed an InMemory-provider
metadata inspection error; relational metadata checks were moved to disconnected Npgsql options,
while InMemory remains limited to behavioral query-filter coverage.

### Runtime Result

Not required; DbContext registration and startup are owned by Phase 08.

### Migration Result

No migration created, as required. Phase 08 owns `InitialDocumentOcr`.

### Remaining Issues

None.
