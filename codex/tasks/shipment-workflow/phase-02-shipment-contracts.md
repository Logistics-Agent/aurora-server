
# Phase 02 — Shipment Contracts

## Status

Completed

## Goal

Create a dedicated contract project for Shipment integration events.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`

## Scope

Required structure:

```text
src/dotnet/Contracts/Shipment.Contracts/
├── Shipment.Contracts.csproj
└── Events/
    ├── ShipmentCreatedEvent.cs
    ├── ShipmentStatusChangedEvent.cs
    └── ShipmentCancelledEvent.cs
```

## Required Events

### ShipmentCreatedEvent

Required fields:

```text
ShipmentId
TenantId
ShipmentNumber
OrderId
CreatedAt
```

### ShipmentStatusChangedEvent

Required fields:

```text
ShipmentId
TenantId
ShipmentNumber
OldStatus
NewStatus
Note
ChangedAt
```

### ShipmentCancelledEvent

Required fields:

```text
ShipmentId
TenantId
ShipmentNumber
Reason
CancelledAt
```

## Constraints

The Contracts project must not contain:

* EF Core dependencies
* DbContext
* Repositories
* Command handlers
* Background workers
* Business logic
* Service-specific runtime configuration

Do not publish events in this phase.

## Validation

Run:

```bash
dotnet build src/dotnet/Contracts/Shipment.Contracts/Shipment.Contracts.csproj
```

## Completion Criteria

* The contract project exists.
* The required event records exist.
* The project has no application or infrastructure dependencies.
* The project builds successfully.

## Work Log

### Completed

* Created `Shipment.Contracts`.
* Referenced the contract project from Shipment Workflow.
* Confirmed that the project builds successfully.

### Files Changed

Record the actual event files present in the repository.

### Commands Executed

```bash
dotnet build src/dotnet/Contracts/Shipment.Contracts/Shipment.Contracts.csproj
```

### Build Result

Build succeeded.

### Remaining Issues

* Verify the final fields of all event records before using them in the outbox flow.
* Events are not yet published.

