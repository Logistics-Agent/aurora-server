# Phase 16 — Document and Milestone Management

## Status

Completed

## Goal

Implement shipment document metadata and business milestone behavior.

## Prerequisites

Phase 15 completed.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

## Existing State

CreateShipment vertical slice is complete through Phase 10. Full Shipment Workflow MVP remains in progress.

## Scope

Implement document metadata attachment, OCR status/confidence metadata, extracted JSON metadata, DocumentAttached event, business milestone recording, GPS/system/user sources, and timeline completion.

## Required Behavior

Shipment Workflow stores metadata and references only; files remain outside this service.

## Constraints

Do not implement OCR execution or object storage ownership unless explicitly assigned.

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

* Added document metadata attach, OCR metadata update, and remove commands.
* Added business milestone creation command.
* Added Phase 16 gRPC RPCs and request messages for document and milestone management.
* Added DocumentAttached integration contract and outbox writes for document attachment.
* Preserved metadata-only ownership; object storage and OCR execution remain out of scope.
* Added controlled OCR status/confidence metadata update with confidence range validation.
* Added milestone source and coordinate validation through the aggregate.
* Preserved tenant resolution from current-user context and cross-tenant NotFound behavior.
* Fixed lifecycle persistence so status histories and transition milestones are inserted while status updates continue to use the tenant-scoped SQL update path.
* Added PostgreSQL-backed tests for documents, milestones, timeline composition, and tenant isolation.

### Implementation Notes

Shipment Workflow stores only document metadata and storage references. OCR services must use controlled contracts; they do not write directly to this database. Business milestones are separate from detailed GPS tracking history, which remains owned by GPS Tracking.

### Files Changed

* `protos/shipment_workflow.proto`
* `src/dotnet/Contracts/Shipment.Contracts/Events/DocumentAttachedEvent.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ManageDocumentCommands.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ManageMilestoneCommands.cs`
* `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ShipmentCommandHelpers.cs`
* `src/dotnet/ShipmentWorkflow/Domain/Shipment.cs`
* `src/dotnet/ShipmentWorkflow/Domain/ShipmentDocument.cs`
* `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
* `tests/dotnet/ShipmentWorkflow.Tests/ShipmentDocumentMilestoneManagementTests.cs`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj --filter ShipmentDocumentMilestoneManagementTests
dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj --filter "ShipmentCommandTests|ShipmentDocumentMilestoneManagementTests"
```

### Build Result

Passed: `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: `dotnet test tests/dotnet/ShipmentWorkflow.Tests/ShipmentWorkflow.Tests.csproj` completed with 73 tests passed, 0 warnings.

### Runtime Result

Not run in Phase 16. Runtime smoke validation remains planned for Phase 19.

### Migration Result

No migration generated or applied in Phase 16. The expanded-schema migration remains planned for Phase 19.

### Remaining Issues

* Shipment import remains Phase 17 scope.
* Full integration event contract completion remains Phase 18 scope.
* Incremental expanded-schema migration and database update remain Phase 19 scope.

### Commit Hash

Recorded in final report from `git log` after the Phase 16 commit is finalized.
