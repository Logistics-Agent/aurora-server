# Phase 09 — Testing

## Status

Not Started

## Goal

Add Compliance tests.

## Prerequisites

Phase 08.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Audit and complete domain, contract, PostgreSQL/vector, retrieval, evaluation, messaging, security,
and runtime coverage for the full Regulatory Compliance RAG MVP.

## Required Behavior

* Cover source/version domain rules, deterministic chunking, embedding validation/idempotency,
  retrieval filters/ranking, citation construction, evaluation rules, and insufficient evidence.
* Add PostgreSQL-backed tests for migration/vector extension, source visibility, unique hashes,
  effective/superseded versions, relationships, JSON/vector persistence, inbox/outbox locking.
* Test all gRPC mappings, missing/client TenantId behavior, cross-tenant access, staff ingestion
  authorization, input/size/topK limits, and unsafe source reference rejection.
* Run deterministic end-to-end ingestion -> embedding -> retrieval -> cited evaluation with fakes.
* Prove concurrent/replayed ingestion and evaluation are idempotent and partial failures are atomic.
* With RabbitMQ available, publish/receive completion/failure events and verify EventId/TenantId plus
  outbox processing state; no OCR/Shipment/Notification process is required.
* Smoke-start with Docker and fake providers. No test may require paid AI credentials or network
  access to live regulatory sources.
* Rebuild/rerun Shipment, Notification, GPS, and Document OCR regressions when OCR exists; record
  unrelated failures rather than weakening another service.
* Inspect migration state, full diff, secrets, generated artifacts, and working tree.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj
dotnet ef migrations list --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Full service definition of done passes with test count, migration/vector, runtime, and broker proof.
* Create local commit `test(compliance): complete service validation`.

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
