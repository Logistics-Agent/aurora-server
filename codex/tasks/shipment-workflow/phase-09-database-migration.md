# Phase 09 — Database Migration

## Status

Completed

## Goal

Create and apply the initial Entity Framework Core migration for Shipment Workflow.

## Prerequisites

* Phase 05 — Persistence
* Phase 07 — Program Configuration
* Phase 08 — Create Shipment Vertical Slice

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`
* Existing migration conventions in the repository

## Scope

Migration output directory:

```text
src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/Migrations/
```

## Required Tables

```text
shipments
cargo_items
shipment_status_histories
outbox_messages
```

## Required Migration Command

Use the repository-compatible form of:

```bash
dotnet ef migrations add InitialShipmentWorkflow \
  --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj \
  --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj \
  --output-dir Infrastructure/Persistences/Migrations
```

## Database Update Command

When local PostgreSQL configuration is available:

```bash
dotnet ef database update \
  --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj \
  --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

## Database Name

Recommended:

```text
aurora_shipment_workflow
```

## Requirements

Before accepting the generated migration, inspect it for:

* Table names
* Column types
* Nullability
* Primary keys
* Foreign keys
* Unique indexes
* Tenant indexes
* Cascade-delete behavior
* Outbox indexes

## Constraints

* Do not manually edit migration code unless required and documented.
* Do not drop an unrelated database.
* Do not use another service's database.
* Do not commit credentials.
* Do not implement new features.

## Validation

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

Run:

```bash
dotnet ef migrations list \
  --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj \
  --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

Apply the database update only when the configured database is confirmed to be the Shipment Workflow database.

## Completion Criteria

* Initial migration exists.
* Migration contains all required tables.
* Indexes are correct.
* Foreign keys are correct.
* Database update succeeds in the intended environment.
* Project still builds successfully.

## Work Log

### Completed

* Inspected existing untracked migration files and found stale namespace metadata plus missing `outbox_messages`.
* Replaced the stale untracked migration files with a fresh EF-generated `InitialShipmentWorkflow` migration from the current model.
* Reviewed generated migration for required tables, indexes, foreign keys, cascade behavior, tenant indexes, and outbox indexes.
* Verified the project still builds.
* Listed migrations successfully.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/Migrations/20260713201248_InitialShipmentWorkflow.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/Migrations/20260713201248_InitialShipmentWorkflow.Designer.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/Migrations/ShipmentWorkflowDbContextModelSnapshot.cs`
* `codex/tasks/shipment-workflow/phase-09-database-migration.md`
* `codex/plan.md`

### Commands Executed

* `git status --short`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `rg "CreateTable|outbox_messages|ShipmentWorkflow.Domain.Shipment|ShipmentWorkflow.Domain.Entities|shipments|cargo_items|shipment_status_histories" src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/Migrations -g '*.cs'`
* `docker compose -f docker-compose.dev.yml ps`
* `dotnet ef migrations add InitialShipmentWorkflow --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --output-dir Infrastructure/Persistences/Migrations`
* `dotnet ef migrations list --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `dotnet ef database update --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `docker exec aurora-shipment-postgres psql -U postgres -d aurora_shipment_workflow -c '\dt'`
* `docker exec aurora-shipment-postgres psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS aurora_shipment_workflow;"` — executed by user
* `docker exec aurora-shipment-postgres psql -U postgres -d postgres -c "CREATE DATABASE aurora_shipment_workflow;"` — executed by user
* `dotnet ef database update --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` — executed by user and verified afterward

### Build Result

Passed. `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 0 errors and 0 warnings.

### Migration Result

Generated. `20260713201248_InitialShipmentWorkflow` contains the required Shipment Workflow tables and indexes and is listed as Pending.

### Database Update Result

Passed after resetting the local development database `aurora_shipment_workflow` and applying the generated migration. `dotnet ef migrations list` now reports `20260713201248_InitialShipmentWorkflow` as applied, and the database contains `shipments`, `cargo_items`, `shipment_status_histories`, `outbox_messages`, and `__EFMigrationsHistory`.

### Remaining Issues

No Phase 09 migration blocker remains. The local development database was reset by explicit user action before applying the migration.

