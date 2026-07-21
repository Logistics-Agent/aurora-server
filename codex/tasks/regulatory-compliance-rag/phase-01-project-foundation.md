# Phase 01 — Project Foundation

## Status

Not Started

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
