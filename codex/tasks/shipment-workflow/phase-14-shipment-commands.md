# Phase 14 — Shipment Commands

## Status

Completed

## Goal

Implement remaining core shipment write operations.

## Prerequisites

Phase 13 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 13. Full Shipment Workflow MVP remains in progress.

## Scope

Implement SubmitShipment, UpdateShipment, UpdateShipmentStatus, CancelShipment, draft deletion, validation, concurrency considerations, and outbox events.

## Required Behavior

Commands enforce state machine rules and create required outbox records.

## Constraints

Do not allow clients to assign arbitrary statuses.

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

* Implemented `SubmitShipmentCommand`, `UpdateShipmentCommand`, `UpdateShipmentStatusCommand`, `CancelShipmentCommand`, and `DeleteDraftShipmentCommand`.
* Added gRPC RPCs for SubmitShipment, UpdateShipment, and DeleteDraftShipment and wired existing UpdateShipmentStatus/CancelShipment RPCs.
* All commands resolve tenant context server-side and reject missing tenant context.
* Cross-tenant mutations return NotFound through tenant filters without leaking existence.
* UpdateShipment allows edits only before operational processing starts.
* UpdateShipmentStatus maps requested statuses to validated state-machine transitions.
* CancelShipment records status and cancellation outbox messages.
* DeleteDraftShipment only deletes draft/created shipments.
* Status-changing command handlers use `ExecuteUpdateAsync` for lifecycle fields, then persist history/milestone/outbox rows in one SaveChanges unit. This avoids the EF status enum alias tracked-update concurrency issue while preserving tenant-scoped updates.

### Files Changed

* `protos/shipment_workflow.proto`
* `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ShipmentCommandHelpers.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentStatusCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CancelShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/DeleteDraftShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Shipment.cs`
* `src/dotnet/ShipmentWorkflow/Tests/ShipmentCommandTests.cs`
* `codex/tasks/shipment-workflow/phase-14-shipment-commands.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow-gap-analysis.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
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
ok dotnet test: 56 tests passed, 0 warnings in 1 projects
```

### Runtime Result

Not run. Phase 14 command behavior was validated through PostgreSQL-backed tests.

### Migration Result

No migration generated or applied in Phase 14.

### Remaining Issues

* Cargo/location management remains Phase 15.
* Document/milestone management remains Phase 16.
* Full event contract expansion remains Phase 18.
