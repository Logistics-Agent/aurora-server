# Phase 01 — Project Foundation

## Status

Completed

## Goal

Create Compliance RAG foundation.

## Prerequisites

None.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Create the .NET 10 `RegulatoryCompliance` Web/gRPC project, colocated test project, and
`RegulatoryCompliance.Contracts` project using the repository and shared-service conventions.
Add only the minimal configuration and package skeleton required by later phases.

## Required Behavior

* Target .NET 10 and exclude `Tests/**/*` from production Web SDK items.
* Reference `shared` from the service and isolate cross-service DTOs in the contracts project.
* Add minimal `Program.cs`, `appsettings.json`, launch profile, and root diagnostic endpoint.
* Reserve `protos/regulatory_compliance.proto`; define final RPCs only in Phase 02.
* Add one foundation test proving production and test assemblies load.
* Do not add LLM/embedding vendors, vector storage, persistence, ingestion, or evaluation logic.

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
* Project layout is repository-compatible and contains no provider credentials.
* Create local commit `feat(compliance): create service foundation`.

## Work Log

### Completed

Created the .NET 10 `RegulatoryCompliance` Web/gRPC project, separate contracts assembly,
colocated xUnit test project, configuration skeleton, HTTP/2 launch profile, root diagnostic
endpoint, and reserved protobuf file. Production compilation explicitly excludes test sources
and no provider, vector, persistence, ingestion, or evaluation implementation was added.

### Files Changed

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/tasks/README.md`
* `protos/regulatory_compliance.proto`
* `src/dotnet/Contracts/RegulatoryCompliance.Contracts/RegulatoryCompliance.Contracts.csproj`
* `src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj`
* `src/dotnet/RegulatoryCompliance/Program.cs`
* `src/dotnet/RegulatoryCompliance/Properties/launchSettings.json`
* `src/dotnet/RegulatoryCompliance/appsettings.json`
* `src/dotnet/RegulatoryCompliance/appsettings.Development.json`
* `src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj`
* `src/dotnet/RegulatoryCompliance/Tests/ServiceFoundationTests.cs`

### Commands Executed

```bash
git status --short --branch
git switch -c feat/regulatory-compliance-rag
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj
```

### Build Result

The pre-implementation build returned expected `MSB1009` because the project did not yet exist.
After implementation, 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 1 foundation test, 0 failed, 0 warnings.

### Runtime Result

Not required for the project-foundation phase.

### Migration Result

No migration or persistence model was created in this phase.

### Remaining Issues

None.
