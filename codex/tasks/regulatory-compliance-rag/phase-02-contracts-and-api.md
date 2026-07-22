# Phase 02 — Contracts and API

## Status

Completed

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

* Defined the four approved MVP RPCs for compliance evaluation, evaluation lookup,
  evidence-backed regulatory queries, and controlled source ingestion.
* Added immutable shipment/cargo/OCR snapshots without a client-controlled TenantId.
* Added explicit evidence sufficiency, findings, citations, lifecycle timestamps, and
  ingestion status contract shapes.
* Added versioned completion and failure integration-event contracts with unique event IDs.
* Added contract tests for the approved RPC surface, tenant/provider field exclusion,
  evidence shapes, protobuf timestamps, and event versioning.

### Files Changed

* `protos/regulatory_compliance.proto`
* `src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj`
* `src/dotnet/RegulatoryCompliance/Tests/RegulatoryComplianceContractTests.cs`
* `src/dotnet/Contracts/RegulatoryCompliance.Contracts/Events/ComplianceEvaluationCompletedEvent.cs`
* `src/dotnet/Contracts/RegulatoryCompliance.Contracts/Events/ComplianceEvaluationFailedEvent.cs`
* `codex/tasks/regulatory-compliance-rag/phase-02-contracts-and-api.md`
* `codex/tasks/README.md`
* `codex/plan.md`

### Commands Executed

* `git status --short`
* `dotnet build src/dotnet/Contracts/RegulatoryCompliance.Contracts/RegulatoryCompliance.Contracts.csproj`
* `dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj`
* `dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj`
* `git diff --check`

### Build Result

Passed: contracts assembly and service build completed with 0 errors and 0 warnings.

### Test Result

Passed: 6 tests, 0 failed.

### Runtime Result

Not required for this contract-only phase.

### Migration Result

Not required; no persistence model or migration changed.

### Remaining Issues

None. Provider implementations, persistence, ingestion, retrieval, and evaluation remain
intentionally assigned to Phases 03-07.
