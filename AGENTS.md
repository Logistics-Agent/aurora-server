# Aurora Server Agent Instructions

## Required Reading

Before inspecting or modifying source code, read:

1. `codex/requirement.md`
2. `codex/plan.md`
3. `codex/specs/logistics-architecture.md`
4. The active service specification under `codex/specs/`
5. The active phase file under `codex/tasks/`
6. Relevant source code, tests, migrations, contracts, and configuration

Treat `codex/requirement.md` as the business and architecture source of truth. Treat `codex/plan.md` as the current execution state.

## Execution Scope

* Execute only the active service and active phase unless the user explicitly overrides the plan.
* The active service is currently Regulatory Compliance RAG.
* Do not start the next service automatically.
* A new service branch may only be created after the current service is fully completed, committed, built, migrated, and tested.
* Production code changes are restricted to services assigned to Ngoc Khoa unless an explicitly required shared contract change is proven necessary.

## Architecture Rules

* Use .NET 10 for .NET services.
* Each microservice owns a separate database.
* Never access another service database directly.
* Do not create cross-service database foreign keys.
* Use gRPC for synchronous service communication.
* Use MassTransit/RabbitMQ for asynchronous integration events.
* Use the transactional outbox pattern for reliable integration-event publication.
* Resolve `TenantId` from authenticated current-user context.
* Never trust client-controlled `TenantId`.
* Preserve tenant isolation in all queries.
* Do not commit credentials, tokens, connection strings for production, or secrets.

## Test Project Layout

* Keep each service test project under `src/dotnet/<Service>/Tests`.
* Exclude `Tests/**/*.cs` from production compilation and `Tests/**/*` from production content/resource item globs; reference the service project from the colocated test project.
* Create test projects for future owned services only when their implementation phase starts.

## Mandatory Workflow

1. Inspect before editing.
2. Run `git status --short`.
3. Run a baseline build for the active service.
4. Run relevant tests when present.
5. Implement only the active phase scope.
6. Build, test, diagnose, and fix until validation passes or a real blocker is identified.
7. Do not suppress valid tests.
8. Update the active phase file and `codex/plan.md` with actual command evidence.
9. Never claim success without executed command evidence.
10. Inspect `git diff` and staged diff before committing.
11. Stage explicit paths only; never use broad staging such as `git add .`, `git add codex`, or `git add src`.
12. Do not push, merge, rebase, reset, clean, or discard user changes unless explicitly instructed.

## Shipment Workflow Namespace Rules

Use:

```text
ShipmentWorkflow.Domain.Entities
ShipmentWorkflow.Domain.Enums
ShipmentWorkflow.Application.Commands.Shipments
ShipmentWorkflow.Application.Queries.Shipments
ShipmentWorkflow.Application.DTOs.Shipments
ShipmentWorkflow.Infrastructure.Persistences
ShipmentWorkflow.Infrastructure.BackgroundJobs
ShipmentWorkflow.GrpcServices
```

Use aliases only where a real unresolved `Shipment` namespace/type collision remains.

## Completion Rules

Mark a phase `Completed` only when its completion criteria pass, required build succeeds, relevant tests pass or absence is recorded, and no unresolved error caused by the phase remains.

When blocked, mark the active phase `Blocked`, record the exact blocker and command, stop dependent work, and report truthfully.
