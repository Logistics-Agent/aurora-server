# Phase 15 — Cargo and Location Management

## Status

Completed

## Goal

Implement cargo and location management behavior.

## Prerequisites

Phase 14 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Implement cargo create/update/remove, location create/update/remove, sequence validation, pickup/delivery requirements, CargoUpdated event, and tenant isolation.

## Required Behavior

Cargo and location changes are validated, tenant-safe, and evented through outbox.

## Constraints

Do not add route geometry ownership.

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

* Added cargo item add, update, and remove commands.
* Added shipment location add, update, and remove commands.
* Added Phase 15 gRPC RPCs and request messages for cargo and location management.
* Preserved tenant resolution through the authenticated current-user context; no command accepts client-controlled TenantId.
* Added pre-operational mutation guard for cargo and location changes.
* Added cargo validation for required name, positive quantity, non-negative weight, optional volume/declared-value/currency constraints.
* Added location sequence validation, duplicate sequence rejection, and coordinate validation.
* Added submit-time validation requiring at least one cargo item plus pickup and delivery locations.
* Added CargoUpdated integration contract and outbox writes for cargo add/update/remove.
* Added PostgreSQL-backed tests for cargo/location management and submit prerequisites.

### Implementation Notes

EF Core treated child entities added through an already-tracked aggregate graph as update candidates because the project uses client-created GUID values while EF metadata marks keys as generated on add. Phase 15 command handlers now explicitly set newly added cargo/location child entries to `Added` and keep the aggregate root unchanged when persisting child/outbox-only mutations.

### Files Changed

* `protos/shipment_workflow.proto`
* `src/dotnet/Contracts/Shipment.Contracts/Events/CargoUpdatedEvent.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ManageCargoCommands.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ManageLocationCommands.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ShipmentCommandHelpers.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/CargoItemDto.cs`
* `src/dotnet/ShipmentWorkflow/Domain/CargoItem.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Shipment.cs`
* `src/dotnet/ShipmentWorkflow/Domain/ShipmentLocation.cs`
* `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs`
* `src/dotnet/ShipmentWorkflow/Tests/ShipmentCargoLocationManagementTests.cs`
* `src/dotnet/ShipmentWorkflow/Tests/ShipmentCommandTests.cs`

### Commands Executed

```bash
git status --short
git diff --stat
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --filter ShipmentCargoLocationManagementTests
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
```

### Build Result

Passed: `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: `dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj` completed with 65 tests passed, 0 warnings.

### Runtime Result

Not run in Phase 15. Runtime smoke validation remains planned for Phase 19.

### Migration Result

No migration generated or applied in Phase 15. The expanded-schema migration remains planned for Phase 19.

### Remaining Issues

* Document and milestone management remains Phase 16 scope.
* Shipment import remains Phase 17 scope.
* Full integration event contract completion remains Phase 18 scope.
* Incremental expanded-schema migration and database update remain Phase 19 scope.

### Commit Hash

Recorded in final report from `git log` after the Phase 15 commit is finalized.
