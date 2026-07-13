# Phase 18 — Contracts and Integration Events

## Status

Not Started

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
