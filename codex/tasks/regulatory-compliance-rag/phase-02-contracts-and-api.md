# Phase 02 — Contracts and API

## Status

Not Started

## Goal

Define compliance APIs/contracts.

## Prerequisites

Phase 01.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Define protobuf and versioned event contracts for compliance evaluation, reading evaluation state,
evidence-backed regulation queries, and controlled regulatory-source ingestion.

## Required Behavior

* Define `EvaluateCompliance`, `GetComplianceEvaluation`, and `QueryRegulations` RPCs plus the
  minimum staff-only regulatory ingestion RPC required by Phase 04.
* Evaluation input carries external Shipment/document IDs and an immutable request snapshot of
  cargo, origin/destination/jurisdictions, transport mode, and OCR structured data; no TenantId.
* Require an idempotency key and explicit jurisdiction/effective-date context.
* Responses expose status, risk level, violations, missing documents, assumptions, confidence,
  and citations containing source/version/section/page/chunk references.
* Query responses distinguish retrieved evidence from generated explanation and must support
  an explicit insufficient-evidence result.
* Use `google.protobuf.Timestamp`; preserve field numbers and enum numeric values after release.
* Add versioned `ComplianceEvaluationCompletedEvent` and `ComplianceEvaluationFailedEvent`
  contracts with EventId, TenantId, EvaluationId, external references, summary, and OccurredAt.
* Never expose embeddings, provider prompts/credentials, EF entities, or internal navigation data.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/Contracts/RegulatoryCompliance.Contracts/RegulatoryCompliance.Contracts.csproj
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Contract tests prove requests cannot control TenantId and every conclusion shape carries evidence.
* Create local commit `feat(compliance): define service contracts`.

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
