# Phase 06 — Extraction Pipeline

## Status

Completed

## Goal

Implement extraction pipeline.

## Prerequisites

Phase 05.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement the application pipeline that claims a queued job, obtains approved content, invokes
the configured OCR provider, normalizes extracted fields, calculates confidence/review state,
persists the result, and creates an outbox event in one transaction.

## Required Behavior

* Resolve TenantId from current-user context for API submissions and use explicit trusted TenantId
  for background processing; never infer a tenant from client JSON.
* Make submission idempotent by `(TenantId, IdempotencyKey)` and return the existing job on replay.
* Validate storage reference, file metadata, supported type/size, and external IDs before enqueueing.
* Transition Queued to Processing through domain methods and prevent concurrent double processing.
* Normalize provider fields to a documented JSON schema with stable field names and optional raw-text
  references; malformed provider output must not be marked completed.
* Calculate overall confidence in `[0,1]` and set `NeedsReview` using a configured threshold plus
  required-field rules; confidence is extraction quality, not compliance confidence.
* Persist completion plus `DocumentOcrCompletedEvent` outbox atomically; record classified failure
  for Phase 07 retry handling and never write directly to Shipment Workflow.
* Implement the Phase 02 gRPC submit/get/list mappings without adding callbacks.

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
* Tests cover idempotent submission, normalization, confidence/review rules, tenant isolation,
  atomic result/outbox writes, provider failures, and API mapping.
* Create local commit `feat(ocr): implement extraction pipeline`.

## Work Log

### Completed

Implemented tenant-context submission, idempotent replay, tenant-safe get/list queries, bounded
pagination, job processing, provider failure classification, stable normalized JSON, confidence/
review rules, completion outbox writes, and all three gRPC mappings. Processing never writes to
Shipment Workflow and does not publish directly.

### Files Changed

* `src/dotnet/DocumentOcr/Application/Jobs/DocumentOcrJobService.cs`
* `src/dotnet/DocumentOcr/Application/Jobs/DocumentOcrResultNormalizer.cs`
* `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs`
* `src/dotnet/DocumentOcr/Infrastructure/Persistences/DocumentOcrDbContext.cs`
* `src/dotnet/DocumentOcr/Tests/Application/DocumentOcrJobServiceTests.cs`
* `src/dotnet/DocumentOcr/Tests/Grpc/DocumentOcrGrpcServiceTests.cs`
* `src/dotnet/DocumentOcr/Tests/Grpc/TestServerCallContext.cs`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj --filter FullyQualifiedName~DocumentOcrJobServiceTests
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 6 focused pipeline tests and 45 total tests, 0 failed, 0 warnings. During development,
InMemory exposed a newly created attempt being tracked as Modified; explicitly adding the domain-
created attempt to its DbSet fixed the root cause while retaining aggregate ownership.

### Runtime Result

Not required; service DI and hosted execution are owned by Phases 07-08.

### Migration Result

No migration created. The status concurrency token is included in the Phase 08 migration model.

### Remaining Issues

None.
