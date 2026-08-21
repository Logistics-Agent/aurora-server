# Phase 03 — Regulatory Document Model

## Status

Completed

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

* Added platform and tenant-private regulatory document identities with explicit visibility
  and deterministic scope keys.
* Added immutable source versions, supersession metadata, ingestion transitions, and ordered chunks.
* Added tenant-owned compliance evaluations, findings, immutable citations, retrieval traces,
  inbox messages, and outbox messages with guarded domain transitions.
* Added PostgreSQL EF mappings, JSONB/enum/precision configuration, aggregate relationships,
  restrictive query filters, uniqueness constraints, and operational indexes.
* Added domain and EF model tests for versioning, chunks, evaluation lifecycle, citation identity,
  confidence, source visibility, missing-tenant behavior, mappings, and indexes.

### Files Changed

* `src/dotnet/RegulatoryCompliance/Domain/Enums/*.cs`
* `src/dotnet/RegulatoryCompliance/Domain/Entities/*.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/Persistences/RegulatoryComplianceDbContext.cs`
* `src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj`
* `src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj`
* `src/dotnet/RegulatoryCompliance/Tests/RegulatoryComplianceDomainTests.cs`
* `src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliancePersistenceModelTests.cs`
* `codex/tasks/regulatory-compliance-rag/phase-03-regulatory-document-model.md`
* `codex/tasks/README.md`
* `codex/plan.md`

### Commands Executed

* `git status --short --branch`
* `dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj`
* `dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj`
* `git diff --check`

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 16 tests, 0 failed, 0 warnings.

### Runtime Result

Not required for the domain/persistence-model phase.

### Migration Result

No migration generated; the initial migration remains owned by Phase 08.

### Remaining Issues

None. Ingestion, embeddings, retrieval, and evaluation orchestration remain sequentially assigned
to Phases 04-07.
