# Phase 11 — Aggregate Expansion

## Status

Completed

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

### Baseline

* `git status --short`: clean working tree.
* `git branch --show-current`: `feat/shipment-workflow`.
* `git log --oneline --decorate -15`: latest commit was `f7fde34 chore(dev): add local shipment infrastructure`.
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`: Passed, 3 projects, 0 errors, 0 warnings.
* `dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj`: Passed, 11 tests, 0 warnings.

### Completed

* Added Shipment aggregate MVP fields: `CustomerId`, priority, transport mode, route and vehicle external IDs, estimated/actual pickup and delivery timestamps, and notes.
* Reused inherited audit fields for `CreatedBy`; no duplicate created-by property was added to `Shipment`.
* Added `ShipmentLocation`, `ShipmentDocument`, and `ShipmentMilestone` tenant-owned child entities.
* Added aggregate methods for adding validated locations, document metadata, and milestones.
* Added enums required by Phase 11: `ShipmentPriority`, `TransportMode`, `LocationType`, `DocumentType`, `OCRStatus`, and `MilestoneSource`.
* Configured EF Core DbSets, relationships, cascade behavior, tenant filters, enum conversions, lengths, precision, and indexes for expanded aggregate entities.
* Preserved `CargoItem` name and existing CreateShipment behavior.
* Did not expose the new fields through gRPC contracts in Phase 11 because the active task only required aggregate expansion and contract compatibility.
* Added tests for valid child entity persistence, invalid location sequence, invalid coordinates, invalid OCR confidence, milestone metadata, child tenant isolation, and existing CreateShipment compatibility.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Domain/Shipment.cs`
* `src/dotnet/ShipmentWorkflow/Domain/ShipmentLocation.cs`
* `src/dotnet/ShipmentWorkflow/Domain/ShipmentDocument.cs`
* `src/dotnet/ShipmentWorkflow/Domain/ShipmentMilestone.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Enums/ShipmentPriority.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Enums/TransportMode.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Enums/LocationType.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Enums/DocumentType.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Enums/OCRStatus.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Enums/MilestoneSource.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs`
* `tests/dotnet/ShipmentWorkflow.Tests/CreateShipmentCommandHandlerTests.cs`
* `tests/dotnet/ShipmentWorkflow.Tests/ShipmentAggregateExpansionTests.cs`
* `tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflowDatabaseCollection.cs`
* `codex/tasks/shipment-workflow/phase-11-aggregate-expansion.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow-gap-analysis.md`

### Commands Executed

```bash
git status --short
git branch --show-current
git log --oneline --decorate -15
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj
rg --files src/dotnet/ShipmentWorkflow
rg --files tests/dotnet/ShipmentWorkflow.Tests
rg --files src/dotnet/Contracts/Shipment.Contracts protos
rg "class Shipment|enum ShipmentStatus|DbSet|HasQueryFilter|EntityTypeBuilder|Create\(" src/dotnet/ShipmentWorkflow tests/dotnet/ShipmentWorkflow.Tests -n
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj
git diff --check
```

### Build Result

Passed. Latest final validation:

```text
ok dotnet build: 3 projects, 0 errors, 0 warnings
```

### Test Result

Passed. Latest final validation:

```text
ok dotnet test: 24 tests passed, 0 warnings in 1 projects
```

### Diff Check Result

Passed. `git diff --check` reported no whitespace errors.

### Runtime Result

Not run. Phase 11 changes are aggregate/model/test scoped and do not add runtime endpoints.

### Migration Result

No migration generated or applied in Phase 11. The expanded EF model intentionally awaits the planned Phase 19 schema migration. Tests use `EnsureCreated` for the expanded model so Phase 11 can validate mappings without recreating the initial migration. Existing migration snapshot still compiles.

### Remaining Issues

* The deployed/local migrated database still needs the Phase 19 expanded-schema migration before the new columns and tables can be used against a migrated database.
* Workflow transition validation remains out of scope for Phase 11 and is planned for Phase 12.
* Query and command handlers for managing locations, documents, and milestones remain out of scope for Phase 11 and are planned for later Shipment phases.
