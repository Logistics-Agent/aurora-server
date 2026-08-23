# Phase 05 — Embedding and Vector Storage

## Status

Completed

## Goal

Implement embedding storage abstraction.

## Prerequisites

Phase 04.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement provider-neutral embedding generation and vector persistence/search primitives for
regulatory chunks, with deterministic local fakes and an Azure-compatible storage boundary.

## Required Behavior

* Define `IEmbeddingProvider` and `IRegulationVectorStore` without leaking vendor SDK types into
  domain/application code; record model name/version and fixed vector dimension.
* Use the repository-approved PostgreSQL vector approach for local MVP when available (for example
  pgvector), while keeping interfaces replaceable by Azure-hosted PostgreSQL/search infrastructure.
* Validate vector dimension, finite values, model version, batch size, timeout, and cancellation.
* Generate embeddings in bounded batches and make writes idempotent by chunk hash plus model version.
* Store only vectors and required provider metadata; never store API keys or prompts in entities/logs.
* Preserve tenant/system source visibility in every vector upsert/search; missing tenant context
  must never broaden access.
* Add deterministic fake embeddings with known similarity ordering for automated tests.
* Do not implement compliance decisions or unsupported free-form generation in this phase.

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
* Tests cover dimension/model validation, deterministic ranking primitives, idempotent re-embedding,
  batching/cancellation/provider failures, and tenant/source visibility.
* Create local commit `feat(compliance): add vector storage`.

## Work Log

### Completed

* Added provider-neutral embedding and vector-store interfaces with fixed model/version/dimension.
* Added deterministic local embeddings, bounded 64-item processing, timeout/cancellation,
  idempotent content/model checks, and recorded provider failures.
* Added tenant/system scope-verified upserts and tenant-filtered deterministic cosine search.
* Stored vectors as PostgreSQL `real[]` behind the replaceable store because pgvector is not
  installed in the repository/local image; candidate search is bounded to 2,000 records.
* Added tests for ranking, dimensions, finite values, model validation, idempotency, batching,
  cancellation, provider failures, and platform/tenant visibility.

### Files Changed

Embedding application interfaces/provider/processor/vector store, chunk vector metadata and EF
mapping, vector tests, and phase/plan documentation.

### Commands Executed

Build, test, Git status/diff, and local pgvector availability inspection commands.

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 27 tests, 0 failed, 0 warnings.

### Runtime Result

Not required until Phase 08.

### Migration Result

No migration generated; Phase 08 owns the initial migration.

### Remaining Issues

No blocker. Native pgvector acceleration is not available in the current repository image;
the replaceable bounded `real[]` store is the approved local fallback for this MVP.
