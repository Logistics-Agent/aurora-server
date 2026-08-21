# Phase 01 — Project Foundation

## Status

Completed

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

Created the .NET 10 Web/gRPC service, colocated xUnit project, contracts project,
reserved protobuf file, root diagnostic endpoint, and secret-free configuration skeleton.

### Files Changed

* `src/dotnet/DocumentOcr/DocumentOcr.csproj`
* `src/dotnet/DocumentOcr/Program.cs`
* `src/dotnet/DocumentOcr/appsettings.json`
* `src/dotnet/DocumentOcr/appsettings.Development.json`
* `src/dotnet/DocumentOcr/Properties/launchSettings.json`
* `src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj`
* `src/dotnet/DocumentOcr/Tests/ServiceFoundationTests.cs`
* `src/dotnet/Contracts/DocumentOcr.Contracts/DocumentOcr.Contracts.csproj`
* `protos/document_ocr.proto`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
git branch --show-current
git log --oneline --decorate -12
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
```

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 1 test, 0 failed, 0 warnings.

### Runtime Result

Not required for the foundation phase.

### Migration Result

Not required; Phase 08 owns the initial migration.

### Remaining Issues

None.
