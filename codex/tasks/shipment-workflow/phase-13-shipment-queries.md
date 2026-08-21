# Phase 13 — Shipment Queries

## Status

Completed

## Goal

Implement tenant-safe read operations.

## Prerequisites

Phase 12 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 12. Full Shipment Workflow MVP remains in progress.

## Scope

Implement GetShipment, ListShipments, pagination, filtering, response mapping, cargo, locations, documents, and milestones where required.

## Required Behavior

Tenant-filtered reads return correct data and never expose another tenant shipment.

## Constraints

Do not implement write commands in this phase.

## Validation Commands

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
git diff --check
```

## Completion Criteria

* Scope is implemented or deliberately deferred with documented reason.
* Shipment Workflow builds successfully.
* Relevant tests pass.
* Tenant isolation is preserved.
* Task file and `codex/plan.md` are updated with command evidence.
* One local commit is created for this phase.

## Work Log

### Completed

* Implemented `GetShipmentQuery`, `ListShipmentsQuery`, and `GetShipmentTimelineQuery`.
* Added tenant context validation for all query paths.
* Added NotFound behavior that does not leak cross-tenant shipment existence.
* Added aggregate DTO mapping for cargo, locations, documents, milestones, lifecycle fields, and timestamps.
* Implemented pagination, status, shipment number, customer, and created-date filters for list queries.
* Implemented deterministic timeline composition from status histories and business milestones.
* Implemented gRPC service methods for GetShipment, ListShipments, and GetShipmentTimeline.
* Extended proto response messages compatibly with new field numbers for aggregate data and filters.

### Files Changed

* `protos/shipment_workflow.proto`
* `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentLocationDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentDocumentDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentMilestoneDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentTimelineDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ListShipmentsResult.cs`
* `src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/GetShipmentQuery.cs`
* `src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/ListShipmentsQuery.cs`
* `src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/GetShipmentTimelineQuery.cs`
* `src/dotnet/ShipmentWorkflow/Tests/ShipmentQueryTests.cs`
* `codex/tasks/shipment-workflow/phase-13-shipment-queries.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow-gap-analysis.md`

### Commands Executed

```bash
git status --short
git log --oneline --decorate -5
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
```

### Build Result

Passed. Latest validation:

```text
ok dotnet build: 3 projects, 0 errors, 0 warnings
```

### Test Result

Passed. Latest validation:

```text
ok dotnet test: 49 tests passed, 0 warnings in 1 projects
```

### Runtime Result

Not run. Phase 13 added query behavior and did not require service startup validation.

### Migration Result

No migration generated or applied in Phase 13.

### Remaining Issues

* Write command RPCs remain out of scope until Phase 14.
* Cargo/location/document/milestone management RPCs remain later phase scope.
