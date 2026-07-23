# Phase 07 — Compliance Evaluation

## Status

Completed

## Goal

Implement compliance evaluation.

## Prerequisites

Phase 06.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement idempotent compliance evaluation over a validated shipment/cargo/document snapshot,
using Phase 06 retrieval evidence to produce findings, missing-document checks, risk, confidence,
assumptions, citations, and integration-event intent.

## Required Behavior

* Resolve TenantId from authenticated context for API calls and use trusted event metadata for
  background input; never trust a request-supplied TenantId or query another service database.
* Validate idempotency key, external Shipment ID, cargo/HS code/dangerous-goods fields, route
  jurisdictions, effective date, and supplied OCR document snapshots.
* Retrieve evidence separately for import/export restrictions, dangerous-goods rules, required
  documents, transport-mode constraints, and jurisdiction-specific checks.
* Use deterministic rule/application logic for required checks. Any model-assisted synthesis stays
  behind an interface and may summarize evidence but cannot create an uncited rule or override it.
* Every violation/requirement must cite one or more retrieved immutable source versions/chunks;
  insufficient or conflicting evidence produces NeedsReview/Unknown, not a false Compliant result.
* Calculate documented risk/confidence, preserve assumptions and missing inputs, and distinguish
  extraction confidence from compliance confidence.
* Make evaluation replay idempotent by tenant plus request key/hash; persist evaluation, findings,
  citations, retrieval trace references, and completion/failure outbox atomically.
* Implement Phase 02 evaluate/get mappings and publish only allowlisted
  `ComplianceEvaluationCompletedEvent`/`ComplianceEvaluationFailedEvent` through outbox.
* Do not mutate Shipment, trigger notifications directly, or make customs/legal guarantees.

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
* Tests cover each check category, cited/insufficient/conflicting evidence, risk/confidence,
  idempotency, tenant isolation, atomic outbox, and API mapping.
* Create local commit `feat(compliance): evaluate shipment compliance`.

## Work Log

### Completed

* Implemented tenant-derived, idempotent evaluation over validated shipment/cargo/OCR snapshots.
* Ran evidence retrieval for import, export, required-document, transport, customs, jurisdiction,
  and dangerous-goods checks with tracked trace references.
* Added cited requirements/warnings, deterministic missing-document rules, documented risk and
  confidence calculation, and explicit Unknown/manual-review behavior for weak evidence.
* Persisted evaluation graph, traces, and allowlisted completion/failure outbox atomically.
* Implemented evaluate/get gRPC mappings and tenant-safe lookup.
* Added evaluation, idempotency, validation, failure, event, mapping, and tenant-isolation tests.

### Files Changed

Evaluation application service/models, retrieval trace attachment, evaluate/get gRPC mapping,
evaluation tests, and phase/plan documentation.

### Commands Executed

Build, test, Git status/diff validation commands.

### Build Result

Passed: 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: 40 tests, 0 failed, 0 warnings.

### Runtime Result

Not required until Phase 08.

### Migration Result

No migration generated; Phase 08 owns the initial migration.

### Remaining Issues

None. Runtime DI, outbox publication worker, migration, and smoke validation remain in Phase 08.
