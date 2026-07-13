# Phase 14 — Shipment Commands

## Status

Not Started

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

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Implement SubmitShipment, UpdateShipment, UpdateShipmentStatus, CancelShipment, draft deletion, validation, concurrency considerations, and outbox events.

## Required Behavior

Commands enforce state machine rules and create required outbox records.

## Constraints

Do not allow clients to assign arbitrary statuses.

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
