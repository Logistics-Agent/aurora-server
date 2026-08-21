# Phase 02 — Contracts and API

## Status

Completed

## Goal

Define OCR API/contracts.

## Prerequisites

Phase 01.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Define the protobuf and versioned integration contracts for submitting an OCR job, reading
job/result state, and notifying consumers when extraction completes or permanently fails.

## Required Behavior

* Define `SubmitDocumentJob`, `GetDocumentJob`, and a bounded tenant-scoped `ListDocumentJobs` RPC.
* Submit accepts document metadata, a trusted storage object reference, document type hint,
  external document/shipment references, and an idempotency key; it never accepts TenantId.
* Do not accept arbitrary local paths or fetch arbitrary callback URLs.
* Responses expose job status, detected type, normalized JSON, confidence in `[0,1]`,
  `NeedsReview`, bounded errors, timestamps, and external references.
* Use `google.protobuf.Timestamp`; preserve field numbers and enum numeric values once defined.
* Add versioned `DocumentOcrCompletedEvent` and `DocumentOcrFailedEvent` contracts with EventId,
  TenantId, JobId, external references, result metadata, OccurredAt, and ContractVersion.
* Do not expose provider-native payloads, credentials, EF entities, or storage contents.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/Contracts/DocumentOcr.Contracts/DocumentOcr.Contracts.csproj
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Contract tests prove TenantId is absent from client requests and field mappings compile.
* Create local commit `feat(ocr): define service contracts`.

## Work Log

### Completed

Defined the three approved tenant-scoped job RPCs, stable job/document enums, bounded pagination
messages, Timestamp lifecycle fields, and version 1 completed/failed integration-event contracts.
The submit request deliberately omits TenantId, callback URLs, arbitrary URLs, and local paths.

### Files Changed

* `protos/document_ocr.proto`
* `src/dotnet/DocumentOcr/DocumentOcr.csproj`
* `src/dotnet/DocumentOcr/Tests/DocumentOcrContractTests.cs`
* `src/dotnet/Contracts/DocumentOcr.Contracts/Events/DocumentOcrCompletedEvent.cs`
* `src/dotnet/Contracts/DocumentOcr.Contracts/Events/DocumentOcrFailedEvent.cs`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
dotnet build src/dotnet/Contracts/DocumentOcr.Contracts/DocumentOcr.Contracts.csproj
```

### Build Result

Passed: contracts and service builds completed with 0 errors and 0 warnings.

### Test Result

Passed: 5 tests, 0 failed, 0 warnings. The initial contract test failed at compile time before
the generated types and event contracts were implemented, then passed after implementation.

### Runtime Result

Not required; RPC handlers are implemented in Phase 06.

### Migration Result

Not required; Phase 08 owns the initial migration.

### Remaining Issues

None.
