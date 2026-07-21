# Phase 01 — Project Foundation

## Status

Not Started

## Goal

Create Document OCR service foundation.

## Prerequisites

None.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Create the .NET 10 `DocumentOcr` Web/gRPC project, colocated test project, and
`DocumentOcr.Contracts` project using the repository layout and shared project conventions.
Add only the minimum package references and configuration skeleton needed by later phases.

## Required Behavior

* Target .NET 10 and exclude `Tests/**/*` from production Web SDK items.
* Reference `shared` from the service and keep cross-service DTOs in the contracts project.
* Add a minimal `Program.cs`, `appsettings.json`, launch profile, and root diagnostic endpoint.
* Reserve `protos/document_ocr.proto`; do not define the final RPC surface before Phase 02.
* Create one foundation test proving the production and test assemblies load.
* Do not add OCR vendors, persistence, background workers, object storage, or business logic.

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
* Project layout matches `src/dotnet/<Service>/Tests` and builds without secrets.
* Create local commit `feat(ocr): create service foundation`.

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
