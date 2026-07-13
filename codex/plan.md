# Aurora Server — Implementation Plan

## 1. Current Goal

Complete Shipment Workflow as a working microservice.

The first vertical slice is:

```text
CreateShipmentRequest
→ ShipmentGrpcService
→ CreateShipmentCommand
→ Shipment.Create()
→ ShipmentWorkflowDbContext
→ PostgreSQL
→ ShipmentResponse
```

Expected results:

* Shipment Workflow builds successfully.
* `CreateShipment` works through gRPC.
* Shipment data is stored in PostgreSQL.
* Tenant isolation works.
* Initial status history is stored.
* Shipment events can later be published through the transactional outbox.

## 2. Phase Status

| Phase | Name                           | Status      |
| ----- | ------------------------------ | ----------- |
| 01    | Project Foundation             | Completed   |
| 02    | Shipment Contracts             | Completed   |
| 03    | Domain Model                   | In Progress |
| 04    | Namespace Cleanup              | Completed   |
| 05    | Persistence                    | Completed   |
| 06    | Proto Contract                 | Completed   |
| 07    | Program Configuration          | Completed   |
| 08    | Create Shipment Vertical Slice | Completed   |
| 09    | Database Migration             | Completed   |
| 10    | Testing                        | Completed   |

## 3. Completed Work

* Created `src/dotnet/ShipmentWorkflow`.
* Created `src/dotnet/Contracts/Shipment.Contracts`.
* Configured .NET 10.
* Referenced `shared`.
* Referenced `Shipment.Contracts`.
* Built `shared` successfully.
* Built `Shipment.Contracts` successfully.
* Created the initial `Shipment` entity.
* Added `Shipment.Create(...)`.
* Added `Shipment.ChangeStatus(...)`.
* Started `CargoItem`.
* Started `ShipmentStatusHistory`.
* Created `ShipmentDto`.
* Started `ShipmentWorkflowDbContext`.
* Added tenant query filters.
* Added indexes.
* Added entity relationships.
* Started `CreateShipmentCommand`.

## 4. Current Work

The requested Shipment Workflow phases are complete through Phase 10 — Testing.

Phase 09 database migration is complete and Phase 10 test coverage passes.

## 5. Phase 04 — Namespace Cleanup

### Status

Completed

### Goal

Resolve all conflicts where `Shipment` is interpreted as a namespace instead of the domain entity.

### Files to Inspect

```text
src/dotnet/ShipmentWorkflow/Domain/Shipment.cs
src/dotnet/ShipmentWorkflow/Domain/CargoItem.cs
src/dotnet/ShipmentWorkflow/Domain/ShipmentStatusHistory.cs
src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentDto.cs
src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs
src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs
```

### Requirements

* All domain entities must use `ShipmentWorkflow.Domain.Entities`.
* Update affected imports and type references.
* Remove obsolete imports.
* Use a fully qualified alias only when necessary.
* Do not change business logic.
* Do not add packages.
* Do not modify unrelated services.
* Do not implement later phases.

Temporary alias:

```csharp
using ShipmentEntity =
    global::ShipmentWorkflow.Domain.Entities.Shipment;
```

Preferred import after the conflict is resolved:

```csharp
using ShipmentWorkflow.Domain.Entities;
```

### Validation

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

### Completion Criteria

* No `CS0118` errors.
* No `CS0234` errors related to Shipment entities.
* No `CS0246` errors related to Shipment entities.
* Shipment Workflow builds successfully.
* Business logic remains unchanged.

### Work Log

#### Completed

* Confirmed active domain entities use `ShipmentWorkflow.Domain.Entities`.
* Verified normal `Shipment` imports still collide in DTO, command, and DbContext code.
* Kept explicit `ShipmentEntity` aliases only at the proven collision points.
* Build passed with 0 errors and 0 warnings.

#### Files Changed

* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ShipmentDto.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs`
* `codex/tasks/phase-04-namespace-cleanup.md`
* `codex/plan.md`

#### Commands Executed

* `git status --short`
* `rg "^namespace |ShipmentWorkflow\.Domain\.Shipment|Domain\.Shipment|class Shipment|class CargoItem|class ShipmentStatusHistory|class OutboxMessage|DbSet<" src/dotnet/ShipmentWorkflow -g '*.cs'`
* `rg --files src/dotnet/ShipmentWorkflow src/dotnet/Contracts protos codex | sort`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` — baseline passed
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` — final Phase 04 build passed
* `find . -type f \( -name '*Tests.csproj' -o -name '*Test.csproj' \) -not -path '*/bin/*' -not -path '*/obj/*'`

#### Build Result

Passed. `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 0 errors and 0 warnings.

#### Remaining Issues

No Phase 04 active-code namespace errors remain. No relevant automated test project was found.

## 6. Remaining Work

### Phase 03 — Domain Model

* Complete `CargoItem`.
* Complete `ShipmentStatusHistory`.
* Create `OutboxMessage`.
* Add validation to `Shipment.Create`.
* Add valid status transition rules.
* Add a shipment cancellation method.

### Phase 05 — Persistence

Status: Completed

* Added `OutboxMessage` domain entity.
* Added `OutboxMessages` DbSet.
* Configured outbox table, primary key, `ProcessedAt` index, `CreatedAt` index, required `EventType`, required `Payload`, and optional `Error`.
* Confirmed existing tenant filters for Shipments, CargoItems, and ShipmentStatusHistories remain configured.
* Confirmed the audit interceptor remains configured.
* Build passed with 0 errors and 0 warnings.
* No relevant automated test project was found.

### Phase 06 — Proto Contract

Status: Completed

* Completed `shipment_workflow.proto`.
* Added all MVP RPC operations, including `CancelShipment`.
* Used `google.protobuf.Timestamp` for timestamp fields.
* Confirmed create requests do not accept `TenantId` from the client.
* Build passed with 0 errors and 0 warnings.

### Phase 07 — Program Configuration

Status: Completed

* Registered gRPC, interceptors, shared services, MassTransit/RabbitMQ, MediatR, DbContext, PostgreSQL, and shipment number generator.
* Mapped `ShipmentGrpcService`.
* Removed template Greeter service/proto from ShipmentWorkflow.
* Confirmed no ShipmentWorkflow outbox worker exists yet, so none was registered.
* Build passed with 0 errors and 0 warnings.
* Startup smoke validation stayed running until manually stopped.

### Phase 08 — Create Shipment

Status: Completed

* Completed `CreateShipmentCommand` for shipment, cargo, status history, and outbox persistence.
* Resolved `TenantId` from current-user context only.
* Added validation for shipment and cargo fields.
* Generated tenant-scoped shipment numbers using `IShipmentNumberGenerator`.
* Implemented gRPC CreateShipment request/response mapping.
* Added a minimal protobuf response update for cargo items.
* Build passed with 0 errors and 0 warnings.
* No relevant automated test project was found; coverage is pending Phase 10.

### Phase 09 — Database Migration

Status: Completed

* Generated `20260713201248_InitialShipmentWorkflow` from the current model.
* Verified required tables, indexes, foreign keys, cascade behavior, tenant indexes, and outbox indexes in the generated migration.
* Reset the local development database by explicit user action.
* Applied the migration to `aurora_shipment_workflow`.
* Verified the database contains `shipments`, `cargo_items`, `shipment_status_histories`, `outbox_messages`, and `__EFMigrationsHistory`.
* Build passed with 0 errors and 0 warnings.

### Phase 10 — Testing

Status: Completed

* Added `ShipmentWorkflow.Tests` xUnit test project.
* Added PostgreSQL-backed integration tests for CreateShipment command handling.
* Covered successful creation, missing tenant context, required-field validation, invalid cargo validation, status history creation, outbox creation, and tenant isolation.
* Fixed tenant query filters so missing tenant context does not disable filtering.
* `dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj` passed with 11 tests.
* Build passed with 0 errors and 0 warnings.

## 7. Immediate Next Actions

Complete these steps in order:

```text
1. Review and commit any remaining project documentation/setup files when desired.
2. Next implementation phase should be explicitly selected by the user.
```

Do not start a later phase before the current phase passes validation.

## 8. Prompt for Codex

Use this prompt from the repository root:

```text
Read the following files before making any changes:

- AGENTS.md
- codex/requirement.md
- codex/plan.md

Execute only Phase 04 — Namespace Cleanup.

Requirements:

1. Inspect the existing Shipment Workflow source code before editing.
2. Identify the exact namespace that conflicts with the Shipment entity.
3. Standardize the Shipment, CargoItem, and ShipmentStatusHistory namespaces.
4. Update all affected imports and type references.
5. Do not change business logic.
6. Do not add packages.
7. Do not modify unrelated services.
8. Do not implement persistence, protobuf, gRPC, migrations, or later phases.
9. Run:

dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj

10. Update codex/plan.md with:
    - Work completed
    - Files changed
    - Commands executed
    - Build result
    - Remaining issues

11. Update the Phase 04 status based on the actual build result.
12. Stop after Phase 04.

At the end, report:

- Root cause
- Changes made
- Files changed
- Build result
- Remaining errors
- Recommended next phase

Do not claim completion unless the build succeeds.
```

## 9. Codex Command

Interactive mode:

```bash
cd ~/project/aurora-server
codex
```

Then paste the prompt from Section 8.

Non-interactive mode:

```bash
codex exec \
  -C ~/project/aurora-server \
  --sandbox workspace-write \
  "Read AGENTS.md, codex/requirement.md, and codex/plan.md. Execute only Phase 04 — Namespace Cleanup. Inspect the existing code, run the required ShipmentWorkflow build, update codex/plan.md with actual results, and stop after this phase."
```
