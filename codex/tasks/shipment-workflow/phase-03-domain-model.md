# Phase 03 — Domain Model

## Status

Partially Completed

## Goal

Complete the minimum domain model required by Shipment Workflow.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`

## Scope

Required files:

```text
src/dotnet/ShipmentWorkflow/Domain/Shipment.cs
src/dotnet/ShipmentWorkflow/Domain/CargoItem.cs
src/dotnet/ShipmentWorkflow/Domain/ShipmentStatusHistory.cs
src/dotnet/ShipmentWorkflow/Domain/OutboxMessage.cs
src/dotnet/ShipmentWorkflow/Domain/Enums/ShipmentStatus.cs
```

## Required Entity Namespace

```csharp
namespace ShipmentWorkflow.Domain.Entities;
```

Enums must use:

```csharp
namespace ShipmentWorkflow.Domain.Enums;
```

## Shipment Requirements

Properties:

```text
Id
TenantId
ShipmentNo
OrderId
CustomerName
DestinationAddress
Status
CargoItems
StatusHistories
CreatedAt
UpdatedAt
```

Methods:

```text
Create(...)
ChangeStatus(...)
Cancel(...)
AddCargoItem(...)
```

## CargoItem Requirements

Properties:

```text
Id
ShipmentId
Name
Quantity
WeightKg
HsCode
Shipment
```

## ShipmentStatusHistory Requirements

Properties:

```text
Id
ShipmentId
Status
Note
CreatedAt
Shipment
```

## OutboxMessage Requirements

Properties:

```text
Id
EventType
Payload
CreatedAt
ProcessedAt
RetryCount
Error
```

## Constraints

* Do not add EF Core configuration in this phase.
* Do not create migrations.
* Do not implement gRPC.
* Do not publish events.
* Do not modify unrelated services.
* Keep domain methods independent of transport-layer types.

## Validation

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

## Completion Criteria

* All required domain files exist.
* Entity namespaces are consistent.
* Shipment creation logic exists.
* Status changes create status history.
* Invalid values are rejected.
* OutboxMessage exists.
* The project compiles past the domain layer.

## Work Log

### Completed

* Created the initial Shipment entity.
* Added `Shipment.Create(...)`.
* Added `Shipment.ChangeStatus(...)`.
* Added cargo and history navigation collections.
* Started CargoItem.
* Started ShipmentStatusHistory.

### Files Changed

Record the actual files after completing this phase.

### Commands Executed

Record commands after completing this phase.

### Build Result

Not completed.

### Remaining Issues

* Namespace conflict remains.
* CargoItem must be verified.
* ShipmentStatusHistory must be verified.
* OutboxMessage has not been confirmed.
* Validation and transition rules are incomplete.
* Cancel and AddCargoItem are incomplete.

