# Phase 11 — Aggregate Expansion

## Status

Not Started

## Goal

Expand the Shipment aggregate to the full logistics MVP shape.

## Prerequisites

Phases 01–10 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Add ShipmentLocation, ShipmentDocument, ShipmentMilestone, missing Shipment fields, required enums, relationships, tenant ownership, domain invariants, persistence mappings, and compatibility analysis.

## Required Behavior

New entities and enums compile, are mapped, and keep existing CreateShipment behavior compatible.

## Constraints

Do not rename CargoItem without documented migration compatibility review. Do not implement query/command behavior beyond what is needed for aggregate integrity.

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
