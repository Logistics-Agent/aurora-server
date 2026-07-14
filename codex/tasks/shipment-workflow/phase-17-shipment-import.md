# Phase 17 — Shipment Import

## Status

Completed

## Goal

Implement staff CSV or Excel import MVP.

## Prerequisites

Phase 16 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Implement row-level validation, import result reporting, small-file synchronous processing, clear large-file limit, tenant isolation, and idempotency where request identifier exists.

## Required Behavior

Small import creates shipments and cargo safely with useful per-row errors.

## Constraints

Do not introduce a complex background-import platform unless required.

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

* Added synchronous CSV import command and gRPC RPC.
* Added row-level import results with total, success, and error counts.
* Enforced CSV-only MVP import with 256 KiB content limit and 100-row limit.
* Rejected client-controlled TenantId columns.
* Resolved TenantId only from current-user context.
* Created valid shipment rows through the Shipment aggregate, including cargo and initial status history.
* Wrote ShipmentCreated outbox messages atomically for successfully imported rows.
* Chose partial-success transaction policy: valid rows are inserted together in one transaction; invalid rows are reported and skipped.
* Added import request identifier echoing for caller-side idempotency/correlation; no persistent import ledger was added because the existing architecture has no import idempotency store yet.
* Added PostgreSQL-backed tests for valid import, mixed rows, missing columns, TenantId rejection, row/file limits, missing tenant context, and outbox creation.

### Files Changed

* `protos/shipment_workflow.proto`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ImportShipmentsCommand.cs`
* `src/dotnet/ShipmentWorkflow/Application/DTOs/Shipments/ImportShipmentsResult.cs`
* `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
* `tests/dotnet/ShipmentWorkflow.Tests/ShipmentImportTests.cs`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj --filter ShipmentImportTests
```

### Build Result

Passed: `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: `dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj` completed with 78 tests passed, 0 warnings.

### Runtime Result

Not run in Phase 17. Runtime smoke validation remains planned for Phase 19.

### Migration Result

No migration generated or applied in Phase 17. Import uses existing Shipment aggregate tables; expanded-schema migration remains planned for Phase 19.

### Remaining Issues

* Full integration event contract completion remains Phase 18 scope.
* Incremental expanded-schema migration and database update remain Phase 19 scope.

### Commit Hash

Recorded in final report from `git log` after the Phase 17 commit is finalized.
