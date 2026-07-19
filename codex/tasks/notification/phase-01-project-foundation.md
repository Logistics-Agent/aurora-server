# Phase 01 — Project Foundation

## Status

Completed

## Goal

Create Notification service project and contracts.

## Prerequisites

None.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/notification.md`

## Existing State

The independent Notification project and initial gRPC contract are established.

## Scope

Independent service project, dedicated database configuration, contract references.

## Required Behavior

Project builds and remains independent from Shipment database.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/Notification/Notification.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.

## Work Log

### Completed

* Created the independent .NET 10 Notification service project.
* Added the initial gRPC contract for notification listing, read state, and caller-owned preferences.
* Added an isolated Notification test project and contract regression test.
* Added development configuration for the dedicated aurora_notification database; no Shipment database configuration or access was introduced.
* Created feat/notification-service from the verified Shipment branch.

### Files Changed

* protos/notification.proto
* src/dotnet/Notification/Notification.csproj
* src/dotnet/Notification/Program.cs
* src/dotnet/Notification/appsettings.json
* src/dotnet/Notification/appsettings.Development.json
* src/dotnet/Notification/Properties/launchSettings.json
* src/dotnet/Notification/Tests/Notification.Tests.csproj
* src/dotnet/Notification/Tests/NotificationContractTests.cs

### Commands Executed

```bash
git status --short
git log --oneline --decorate -20
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
git switch -c feat/notification-service
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
dotnet build src/dotnet/Notification/Notification.csproj
```

### Build Result

Passed: dotnet build src/dotnet/Notification/Notification.csproj completed with 3 projects, 0 errors, 0 warnings.

### Test Result

Passed: dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj completed with 1 test passing, 0 warnings.

### Runtime Result

Not run. Runtime registration is Phase 07 scope.

### Migration Result

Not run. Initial database migration is Phase 08 scope.

### Remaining Issues

* Phase 02 must define the domain model and its invariants before persistence is introduced.
* A live Shipment-to-Notification flow remains dependent on Shipment's broker-publishing outbox worker; this phase does not access or change the Shipment database.

### Commit Hash

Recorded by the Phase 01 Git commit.
