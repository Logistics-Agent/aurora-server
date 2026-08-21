# Phase 04 — Ingestion and Chunking

## Status

Not Started

## Goal

Implement ingestion/chunking.

## Prerequisites

Phase 03.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement controlled regulatory-source ingestion from approved content/storage references through
normalization, immutable version creation, deterministic chunking, and embedding-work scheduling.

## Required Behavior

* Restrict ingestion to established staff/system authorization; resolve tenant/system scope from
  trusted context and never accept an unrestricted client TenantId.
* Validate authority, canonical source, jurisdiction, language, regulation type, effective dates,
  content type, size, and content hash before parsing.
* Do not fetch arbitrary URLs/local paths and do not perform OCR; scanned documents must arrive
  through the Document OCR contract or approved content reader.
* Make ingestion idempotent by source/version/content hash. Identical replay returns the existing
  version; changed content creates a new immutable version without deleting citation history.
* Normalize whitespace/encoding while preserving section headings, page labels, article numbers,
  and offsets required for citations.
* Chunk deterministically with configured size/overlap and stable sequence/hash; chunks retain
  exact document/version/source/section/page identity.
* Save document version and chunks atomically, then mark chunks pending embedding. Partial parsing
  failure must not publish an active incomplete version.
* Implement the controlled ingestion RPC from Phase 02; do not implement retrieval/evaluation yet.

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
* Tests cover idempotent replay/new version, deterministic chunks, metadata/citation retention,
  malformed/oversized content, unsafe references, authorization, and atomic failure.
* Create local commit `feat(compliance): ingest regulatory sources`.

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
