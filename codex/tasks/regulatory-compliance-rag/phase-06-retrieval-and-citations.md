# Phase 06 — Retrieval and Citations

## Status

Not Started

## Goal

Implement retrieval with citations.

## Prerequisites

Phase 05.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement bounded evidence retrieval and citation construction over active regulatory versions,
using semantic similarity plus mandatory jurisdiction/effective-date/source filters.

## Required Behavior

* Validate query text, jurisdiction, effective date, language/regulation filters, `topK`, and score
  threshold; enforce configured maximums and deterministic ordering/tie-breaking.
* Embed the query through the provider boundary and search only active versions visible to the
  current tenant/system scope.
* Filter out regulations not effective for the requested date or superseded without applicability.
* Return each evidence item with document/version/chunk IDs, authority, title, canonical source,
  section/page labels, effective dates, relevance score, and a bounded excerpt.
* Deduplicate overlapping chunks while retaining enough context for audit and later evaluation.
* `QueryRegulations` must distinguish no evidence/insufficient evidence from a supported answer;
  generated summaries may not introduce claims absent from returned citations.
* Record a bounded retrieval trace (query hash, filters, model version, selected chunk IDs/scores)
  without storing secrets or unnecessary sensitive cargo text.
* Do not make a shipment compliance decision; Phase 07 owns evaluation.

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
* Tests cover filters, effective/superseded versions, stable ranking, overlap deduplication,
  citations, insufficient evidence, tenant isolation, and trace persistence.
* Create local commit `feat(compliance): retrieve cited regulations`.

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
