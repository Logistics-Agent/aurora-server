# Phase 01 — Project Foundation

## Status

Not Started

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

Not started.

### Files Changed

None.

### Commands Executed

None.

### Build Result

Not started.

### Test Result

Not started.

### Runtime Result

Not started.

### Migration Result

Not started.

### Remaining Issues

Phase has not started.
