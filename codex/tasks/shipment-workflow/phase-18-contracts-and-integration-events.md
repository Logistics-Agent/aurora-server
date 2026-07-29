# Phase 18 — Contracts and Integration Events

## Status

Completed

## Goal

Complete Shipment Workflow event contracts and outbox serialization.

## Prerequisites

Phase 17 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Add/adjust contracts for ShipmentCreated, ShipmentSubmitted, ShipmentUpdated, ShipmentCancelled, ShipmentStatusChanged, CargoUpdated, DocumentAttached, RouteAssigned, ShipmentPickedUp, ShipmentDelivered, ShipmentCompleted, event versioning, and consumer-safe schemas.

## Required Behavior

Required events are serialized consistently through outbox without breaking existing consumers unnecessarily.

## Constraints

Do not modify other service code without explicit approval.

## Validation Commands

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj
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

* Added versioned, consumer-safe event contracts for ShipmentSubmitted, ShipmentUpdated, RouteAssigned, ShipmentPickedUp, ShipmentDelivered, and ShipmentCompleted.
* Added EventId and ContractVersion to existing ShipmentCreated, ShipmentStatusChanged, ShipmentCancelled, CargoUpdated, and DocumentAttached events without removing existing fields.
* Added ShipmentSubmitted outbox writes to submit command flow.
* Added ShipmentUpdated outbox writes to update command flow.
* Added lifecycle-specific outbox writes for picked up, delivered, and completed status transitions.
* Preserved existing ShipmentCreated, ShipmentStatusChanged, ShipmentCancelled, CargoUpdated, and DocumentAttached outbox behavior.
* Added serialization and outbox tests for event IDs, versions, tenant preservation, status-change behavior, update behavior, and lifecycle event behavior.

### Implementation Notes

RouteAssignedEvent is defined for consumers, but no route-assignment command exists in the current Shipment Workflow command surface. Route assignment publishing remains available for a future command path without implementing Route Planning service behavior.

### Files Changed

* `src/dotnet/Contracts/Shipment.Contracts/Events/CargoUpdatedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/DocumentAttachedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/RouteAssignedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentCancelledEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentCompletedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentCreatedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentDeliveredEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentPickedUpEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentStatusChangedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentSubmittedEvent.cs`
* `src/dotnet/Contracts/Shipment.Contracts/Events/ShipmentUpdatedEvent.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ShipmentCommandHelpers.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentStatusCommand.cs`
* `tests/dotnet/ShipmentWorkflow.Tests/ShipmentIntegrationEventTests.cs`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj --filter ShipmentIntegrationEventTests
```

### Build Result

Passed: `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: `dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj` completed with 83 tests passed, 0 warnings.

### Runtime Result

Not run in Phase 18. Runtime smoke validation remains planned for Phase 19.

### Migration Result

No migration generated or applied in Phase 18. Contract/event changes do not require a database migration.

### Remaining Issues

* Incremental expanded-schema migration and full MVP validation remain Phase 19 scope.

### Commit Hash

Recorded in final report from `git log` after the Phase 18 commit is finalized.
