
# Phase 05 — Persistence

## Status

Completed

## Goal

Complete Entity Framework Core persistence configuration for Shipment Workflow.

## Prerequisites

The following phases must be completed first:

* Phase 03 — Domain Model
* Phase 04 — Namespace Cleanup

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`

## Scope

Primary file:

```text
src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs
```

Additional domain file:

```text
src/dotnet/ShipmentWorkflow/Domain/OutboxMessage.cs
```

## Required DbSets

```text
Shipments
CargoItems
ShipmentStatusHistories
OutboxMessages
```

## Shipment Configuration

Table:

```text
shipments
```

Required configuration:

```text
Primary key: Id
Unique index: TenantId + ShipmentNo
Index: TenantId + Status
Index: TenantId + CreatedAt
Index: OrderId
ShipmentNo max length: 50
OrderId max length: 100
CustomerName max length: 200
DestinationAddress max length: 500
Status stored as string
```

## CargoItem Configuration

Table:

```text
cargo_items
```

Required configuration:

```text
Primary key: Id
Index: ShipmentId
Name max length: 200
HsCode max length: 50
Quantity required
WeightKg required
```

## ShipmentStatusHistory Configuration

Table:

```text
shipment_status_histories
```

Required configuration:

```text
Primary key: Id
Index: ShipmentId + CreatedAt
Status stored as string
Note max length: 500
```

## OutboxMessage Configuration

Table:

```text
outbox_messages
```

Required configuration:

```text
Primary key: Id
Index: ProcessedAt
Index: CreatedAt
EventType required
Payload required
Error optional
```

## Relationships

```text
Shipment 1 → many CargoItems
Shipment 1 → many ShipmentStatusHistories
```

## Tenant Filters

Shipment:

```text
Shipment.TenantId == CurrentUser.TenantId
```

Cargo item:

```text
CargoItem.Shipment.TenantId == CurrentUser.TenantId
```

Status history:

```text
ShipmentStatusHistory.Shipment.TenantId == CurrentUser.TenantId
```

## Constraints

* Do not create migrations in this phase.
* Do not implement gRPC.
* Do not publish events.
* Do not modify another service.
* Preserve the existing audit interceptor.
* Avoid silently disabling tenant filtering.

## Validation

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

## Completion Criteria

* All required DbSets exist.
* All entities are mapped.
* Required indexes exist.
* Relationships compile.
* Tenant filters are applied.
* Audit interceptor remains configured.
* Shipment Workflow builds successfully.

## Work Log

### Completed

* Added `OutboxMessage` domain entity under `ShipmentWorkflow.Domain.Entities`.
* Added `OutboxMessages` DbSet to `ShipmentWorkflowDbContext`.
* Configured outbox table, primary key, `ProcessedAt` and `CreatedAt` indexes, required `EventType`, required `Payload`, and optional `Error`.
* Confirmed existing shipment, cargo item, and status history mappings and tenant filters remain configured.
* Preserved the existing audit interceptor configuration.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Domain/OutboxMessage.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs`
* `codex/tasks/shipment-workflow/phase-05-persistence.md`
* `codex/plan.md`

### Commands Executed

* `rg "class AuditableEntity|class TenantAuditableEntity|CreatedAt|UpdatedAt|AuditSaveChangesInterceptor|DbContext|OutboxMessage" src/dotnet/shared src/dotnet/ShipmentWorkflow -g '*.cs'`
* `cat src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `cat src/dotnet/shared/Entity/AuditableEntity.cs`
* `cat src/dotnet/shared/Interceptors/AuditSaveChangesInterceptor.cs`
* `cat src/dotnet/IamTenant/Domain/OutboxMessage.cs`
* `sed -n '1,170p' src/dotnet/IamTenant/Infrastructure/Persistences/IamTenantDbContext.cs`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `find . -type f \( -name '*Tests.csproj' -o -name '*Test.csproj' \) -not -path '*/bin/*' -not -path '*/obj/*'`

### Build Result

Passed. `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 0 errors and 0 warnings.

### Remaining Issues

No Phase 05 build errors remain. No relevant automated test project was found. No migration was created, per Phase 05 constraints.
