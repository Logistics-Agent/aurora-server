# Phase 12 — Workflow State Machine

## Status

Completed

## Goal

Implement complete shipment lifecycle transition validation.

## Prerequisites

Phase 11 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 11. Full Shipment Workflow MVP remains in progress.

## Scope

Add full statuses, allowed transitions, Submit, Planning, Negotiating, Confirmed, PickedUp, InTransit, CustomsProcessing, Delivered, Completed, Cancelled, domain events, and milestone creation from business transitions.

## Required Behavior

Clients cannot assign arbitrary states; every transition is validated and records the appropriate history/milestone/outbox intent.

## Constraints

Do not implement unrelated query/list/import features.

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

* Added MVP workflow statuses while preserving existing enum numeric compatibility for `Created`, customs, transit, delivery, completion, and cancellation values.
* Implemented explicit allowed transition map in the Shipment aggregate.
* Added domain methods for submit, planning, negotiation, confirm, picked up, in transit, customs processing, delivered, completed, cancelled, route assignment, and vehicle assignment.
* Every successful transition creates status history and a business milestone.
* Pickup and delivery transitions record actual timestamps.
* Completed and Cancelled states are terminal.
* Existing `Created` status remains compatible as the Draft starting state.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Domain/Enums/ShipmentStatus.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Shipment.cs`
* `src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflowStateMachineTests.cs`
* `codex/tasks/shipment-workflow/phase-12-workflow-state-machine.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow-gap-analysis.md`

### Commands Executed

```bash
git status --short
git branch --show-current
git log --oneline --decorate -20
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
rg --files src/dotnet/ShipmentWorkflow src/dotnet/ShipmentWorkflow/Tests src/dotnet/Contracts/Shipment.Contracts protos
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

### Build Result

Passed. Latest validation:

```text
ok dotnet build: 3 projects, 0 errors, 0 warnings
```

### Test Result

Passed. Latest validation:

```text
ok dotnet test: 41 tests passed, 0 warnings in 1 projects
```

### Runtime Result

Not run. Phase 12 is domain state-machine behavior and does not add runtime endpoints.

### Migration Result

No migration generated or applied in Phase 12. Enum storage remains string-based and schema expansion remains Phase 19 scope.

### Remaining Issues

* Outbox event creation for transition commands is implemented in later command/event phases.
* Query and command APIs remain out of scope for Phase 12.
