# Phase 19 — Migration and Full MVP Testing

## Status

Not Started

## Goal

Create expanded aggregate migration and full Shipment MVP regression coverage.

## Prerequisites

Phase 18 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Generate incremental migration, validate existing data compatibility, update DB, add domain/command/query/tenant/state/cancel/timeline/document/cargo/location/import/outbox tests, and run full regression.

## Required Behavior

Full Shipment Workflow MVP builds, migrates, runs relevant tests, and preserves CreateShipment behavior.

## Constraints

Do not generate another initial migration. Stop before destructive DB actions.

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
