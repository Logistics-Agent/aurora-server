# Phase 03 — Regulatory Document Model

## Status

Not Started

## Goal

Define regulatory document model.

## Prerequisites

Phase 02.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement the service-owned regulatory corpus and evaluation domain models, including auditable
source/version metadata, chunks, retrieval traces, findings/citations, inbox, and outbox intent.

## Required Behavior

* Model `RegulatoryDocument` identity separately from immutable `RegulatoryDocumentVersion` content.
* Require authority, canonical source URI, jurisdiction, regulation type, language, published/effective
  dates, content hash, ingestion status, and supersession metadata.
* Model chunks with deterministic sequence, section/page labels, normalized text, token/character
  counts, content hash, and exact version ownership.
* Model `ComplianceEvaluation` with tenant ownership, external Shipment reference, request hash,
  status, risk level, confidence, assumptions, findings, missing documents, and timestamps.
* Model findings/citations so each supported conclusion points to immutable document version and chunk;
  external service IDs remain plain references with no cross-service relationships.
* Define explicit ingestion/evaluation states and domain methods; reject invalid transitions,
  confidence outside `[0,1]`, missing citation identity, and unrestricted public mutation.
* Configure `RegulatoryComplianceDbContext`, mappings, internal relationships, JSON/enum storage,
  and restrictive tenant filters for tenant-owned data; platform regulatory sources require an
  explicit system visibility model and must never expose another tenant's private source.
* Add unique source/version/content-hash keys plus ingestion, jurisdiction/effective-date,
  evaluation, inbox, and outbox worker indexes. Keep vectors abstract until Phase 05.
* Add EF model tests and do not generate a migration; Phase 08 owns the initial migration.

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
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Tests cover source versioning, chunk identity, evaluation transitions, citation invariants,
  confidence/risk validation, mappings/indexes, and restrictive tenant/source visibility.
* Create local commit `feat(compliance): model regulatory data`.

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
