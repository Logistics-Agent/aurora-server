# Phase 08 — Create Shipment Vertical Slice

## Status

Completed

## Goal

Implement the first complete Shipment Workflow use case from gRPC request to database response.

## Prerequisites

* Phase 03 — Domain Model
* Phase 04 — Namespace Cleanup
* Phase 05 — Persistence
* Phase 06 — Proto Contract
* Phase 07 — Program Configuration

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`

## Scope

Likely files:

```text
src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs
src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentDto.cs
src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/CargoItemDto.cs
src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs
```

Related domain and persistence files may be updated only when required by this flow.

## Required Flow

```text
CreateShipmentRequest
→ ShipmentGrpcService
→ CreateShipmentCommand
→ Shipment.Create()
→ add CargoItems
→ add initial ShipmentStatusHistory
→ add ShipmentCreatedEvent OutboxMessage
→ SaveChangesAsync
→ ShipmentResponse
```

## Tenant Requirements

* Resolve TenantId from `ICurrentUserService`.
* Reject requests without a valid tenant context.
* Do not accept TenantId from the request.

## Shipment Number

Shipment number generation must:

* Produce a non-empty value.
* Be unique per tenant.
* Avoid relying only on a low-resolution timestamp when collisions are possible.
* Be implemented behind a dedicated service when the existing architecture supports it.

## Validation Rules

* Customer name is required.
* Destination address is required.
* Cargo item name is required.
* Cargo quantity must be greater than zero.
* Cargo weight must not be negative.
* Tenant context is required.

## Initial Status

New shipments must use:

```text
Created
```

An initial status history record must be stored.

## Outbox

A `ShipmentCreatedEvent` must be serialized into an outbox record.

The shipment, child records, status history, and outbox record must be saved in the same database operation or transaction.

Do not publish directly from the command handler.

## Constraints

* Implement only CreateShipment.
* Do not implement GetShipment, ListShipments, UpdateStatus, Timeline, or CancelShipment.
* Do not create the database migration in this phase.
* Do not modify unrelated services.

## Validation

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

Run relevant unit tests when present.

## Completion Criteria

* gRPC request maps to a command.
* TenantId comes from current-user context.
* Shipment is created.
* Cargo items are created.
* Initial status history is created.
* Outbox message is created.
* Changes are saved.
* Response is returned.
* Project builds successfully.

## Work Log

### Completed

* Extended CreateShipment command to accept cargo items.
* Resolved `TenantId` only from `ICurrentUserService`.
* Added required-field and cargo validation.
* Generated shipment numbers through `IShipmentNumberGenerator` and checked tenant-scoped uniqueness before insert.
* Created shipment cargo items.
* Created initial `Created` status history.
* Serialized `ShipmentCreatedEvent` into an outbox record.
* Saved shipment, cargo, history, and outbox in one EF transaction.
* Mapped gRPC CreateShipment request and response, including cargo items.
* Added a minimal protobuf compatibility update for cargo response fields required by the CreateShipment response.
* Included previously untracked ShipmentWorkflow/Shipment.Contracts source files required for the committed CreateShipment vertical slice to build.

### Files Changed

* `protos/shipment_workflow.proto`
* `src/dotnet/Contracts/Shipment.Contracts/`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/CargoItemDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/Interfaces/IShipmentNumberGenerator.cs`
* `src/dotnet/ShipmentWorkflow/Domain/`
* `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Services/ShipmentNumberGenerator.cs`
* `codex/tasks/phase-08-create-shipment.md`
* `codex/plan.md`

### Commands Executed

* `git status --short`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `find . -type f \( -name '*Tests.csproj' -o -name '*Test.csproj' \) -not -path '*/bin/*' -not -path '*/obj/*'`

### Build Result

Passed. `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 0 errors and 0 warnings.

### Test Result

No relevant automated test project existed at this phase.

### Remaining Issues

No Phase 08 build errors remain. Automated coverage is still pending Phase 10.

