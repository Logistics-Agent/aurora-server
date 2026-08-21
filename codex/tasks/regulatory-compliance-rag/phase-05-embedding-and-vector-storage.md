# Phase 05 — Embedding and Vector Storage

## Status

Not Started

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
