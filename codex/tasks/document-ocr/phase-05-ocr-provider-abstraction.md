# Phase 05 — OCR Provider Abstraction

## Status

Completed

## Goal

Add provider interfaces.

## Prerequisites

Phase 04.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Define provider-neutral OCR and document-content interfaces, normalized provider request/response
models, error classification, and deterministic fakes. Add a real adapter only when the repository
already supplies the provider dependency and local configuration can remain secret-free.

## Required Behavior

* Define `IOcrProvider` without leaking vendor SDK types into application/domain layers.
* Define a controlled `IDocumentContentReader` or equivalent that resolves approved object-storage
  references; never fetch arbitrary client URLs or local filesystem paths.
* Provider responses include detected type, text/layout references, extracted fields, confidence,
  provider request ID, and diagnostics with strict size limits.
* Classify transient, permanent, invalid-document, unsupported-format, and cancellation failures.
* Enforce supported MIME types/extensions and maximum document bytes/pages before provider calls.
* Add a deterministic fake provider/content reader for all automated tests.
* Do not embed paid credentials, choose a provider through domain code, or implement compliance rules.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Tests cover provider success, cancellation, transient/permanent errors, size/type rejection,
  and prevention of arbitrary URL/path access.
* Create local commit `feat(ocr): add provider abstraction`.

## Work Log

### Completed

Defined vendor-neutral OCR/content interfaces, bounded provider models, explicit failure kinds,
document limits, and an approved object-key policy. Added deterministic local adapters that do not
access network, object storage, or the local filesystem and require no paid credentials.

### Files Changed

* `src/dotnet/DocumentOcr/Application/Providers/DocumentProcessingOptions.cs`
* `src/dotnet/DocumentOcr/Application/Providers/DocumentInputPolicy.cs`
* `src/dotnet/DocumentOcr/Application/Providers/IDocumentContentReader.cs`
* `src/dotnet/DocumentOcr/Application/Providers/IOcrProvider.cs`
* `src/dotnet/DocumentOcr/Application/Providers/ProviderModels.cs`
* `src/dotnet/DocumentOcr/Infrastructure/Providers/DeterministicDocumentContentReader.cs`
* `src/dotnet/DocumentOcr/Infrastructure/Providers/DeterministicOcrProvider.cs`
* `src/dotnet/DocumentOcr/Tests/OcrProviderAbstractionTests.cs`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj --filter FullyQualifiedName~OcrProviderAbstractionTests
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 16 focused provider/security cases and 36 total tests, 0 failed, 0 warnings.

### Runtime Result

Not required; adapters are wired into the process in Phase 08.

### Migration Result

Not required; no persistence changes in this phase.

### Remaining Issues

None. A real provider adapter remains intentionally deferred until an approved provider and
secret-backed runtime configuration are supplied; the deterministic adapter supports local/tests.
