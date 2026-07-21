# Phase 07 — Monitoring Rules

## Status

Completed

## Goal

Implement monitoring rules.

## Prerequisites

Phase 06.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/gps-tracking.md`

## Existing State

Production implementation has not started for this service.

## Scope

Implement circular geofence APIs/state, ingestion-time abnormal-stop/geofence evaluation,
and a bounded signal-loss background scan.

## Required Behavior

* Geofences validate name, centre, radius (0-100 km), optional vehicle/shipment scope,
  active state, and tenant ownership.
* Use a deterministic Haversine calculation and persisted presence state to emit entry or
  exit only when state changes.
* Track stationary time from current snapshots; raise abnormal-stop only after configured
  low-speed duration.
* Raise signal-loss only for active assignments whose latest reading exceeds the configured
  threshold.
* Deduplicate active alerts and allow tenant users to list and resolve them.
* Persist alert plus `GpsMonitoringAlertRaisedEvent` outbox message atomically.
* Keep thresholds/options validated on startup and scans bounded.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet build src/dotnet/GpsTracking/GpsTracking.csproj
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Tests cover geofence transitions, distance boundaries, abnormal-stop timing, signal loss,
  deduplication, resolution, and tenant filtering.
* Create local commit `feat(gps): add monitoring rules`.

## Work Log

### Completed

Implemented configurable monitoring thresholds, circular Haversine geofences, persisted
presence state, abnormal-stop detection, bounded starvation-safe signal-loss scans, active
alert deduplication/resolution, and tenant-scoped geofence and alert gRPC operations.
Position ingestion invokes monitoring only when a reading advances the current snapshot,
so late readings remain history-only. Alert records and `GpsMonitoringAlertRaisedEvent`
outbox messages are saved with the ingest or scan transaction.

### Files Changed

* `src/dotnet/GpsTracking/Application/Ingestion/PositionIngestionService.cs`
* `src/dotnet/GpsTracking/Application/Monitoring/GeofenceDistanceCalculator.cs`
* `src/dotnet/GpsTracking/Application/Monitoring/MonitoringAlertWriter.cs`
* `src/dotnet/GpsTracking/Application/Monitoring/MonitoringManagementService.cs`
* `src/dotnet/GpsTracking/Application/Monitoring/MonitoringOptions.cs`
* `src/dotnet/GpsTracking/Application/Monitoring/PositionMonitoringService.cs`
* `src/dotnet/GpsTracking/Application/Monitoring/SignalLossMonitor.cs`
* `src/dotnet/GpsTracking/Domain/Entities/CurrentLocation.cs`
* `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs`
* `src/dotnet/GpsTracking/Infrastructure/BackgroundJobs/SignalLossMonitoringBackgroundService.cs`
* `src/dotnet/GpsTracking/Tests/Application/MonitoringManagementServiceTests.cs`
* `src/dotnet/GpsTracking/Tests/Application/MonitoringServiceTests.cs`
* `src/dotnet/GpsTracking/Tests/Application/PositionIngestionServiceTests.cs`
* `src/dotnet/GpsTracking/Tests/Grpc/GpsTrackingGrpcServiceTests.cs`

### Commands Executed

* `dotnet build src/dotnet/GpsTracking/GpsTracking.csproj --no-restore --verbosity minimal`
* `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore --logger console;verbosity=minimal`
* `git diff --check`

### Build Result

Passed: 4 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 38 tests, 0 failed, 0 skipped. Coverage includes geofence boundaries and state
changes, abnormal-stop timing/resolution, signal-loss tenant isolation/deduplication/batch
progress, management tenant isolation, and ingestion monitoring wiring.

### Runtime Result

Background worker registration and process startup are intentionally deferred to Phase 09.

### Migration Result

No migration generated. Phase 07 uses the Phase 03 model; the initial GPS migration remains
scheduled for Phase 09.

### Remaining Issues

No Phase 07 blocker. Realtime outbox publication is the Phase 08 scope.
