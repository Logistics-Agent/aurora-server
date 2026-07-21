# Phase 01 — Project Foundation

## Status

Completed

## Goal

Create GPS Tracking service foundation.

## Prerequisites

None.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Create `src/dotnet/GpsTracking/GpsTracking.csproj`, a minimal `Program.cs`, safe
configuration templates, launch settings, and the colocated
`src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj`.

## Required Behavior

* Target .NET 10 and follow the existing Web SDK service layout.
* Exclude `Tests/**/*` from production compile/content/resource globs.
* Reference `shared` and Shipment contracts only; do not copy shared/IAM code.
* Add only packages required by the approved architecture.
* Map a minimal root endpoint; defer business RPCs, persistence, messaging, and workers.
* Both production and test projects restore and build independently.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet build src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj
git diff --check
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* No production behavior from Phase 02 or later is implemented.
* Create local commit `feat(gps): create service foundation`.

## Work Log

### Completed

Created the .NET 10 GPS Tracking Web SDK project, minimal HTTP/2 service host, safe
configuration, launch profile, and colocated xUnit test project. Production item globs
exclude all test source and artifacts.

### Files Changed

* `src/dotnet/GpsTracking/GpsTracking.csproj`
* `src/dotnet/GpsTracking/Program.cs`
* `src/dotnet/GpsTracking/appsettings.json`
* `src/dotnet/GpsTracking/Properties/launchSettings.json`
* `src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj`
* `src/dotnet/GpsTracking/Tests/ServiceFoundationTests.cs`

### Commands Executed

* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj`

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 1 test, 0 failed, 0 warnings.

### Runtime Result

Not required for the foundation phase.

### Migration Result

Not required; persistence begins in Phase 03 and migration is reserved for Phase 09.

### Remaining Issues

No Phase 01 issues. Business contracts and domain behavior remain intentionally deferred
to Phase 02.
