# Implementation Plan: GPS Simulator

## Overview

Fix and extend the existing `tools/gps-simulator/Program.cs`. The simulator already has the correct route, gRPC plumbing, and request builder — these 10 tasks address bugs, missing validation, missing env-var support, graceful cancellation, and success/failure tracking.

## Tasks

- [ ] 1. Fix env/default bugs
  - [ ] 1.1 Fix `GPS_GRPC_URL` default from `"http://localhost:5004"` to `"http://localhost:6002"` in `Program.cs`
  - [ ] 1.2 Fix `TENANT_ID` default from `"00000000-0000-0000-0000-000000000001"` to `"01920000-0000-7000-8000-000000000001"` in `Program.cs`
  - [ ] 1.3 Add `userId` variable from `USER_ID` env var with default `"01910000-0000-7000-8000-000000000001"` in `Program.cs`
  - [ ] 1.4 Add `shipmentGrpcUrl` variable from `SHIPMENT_GRPC_URL` env var with default `"http://localhost:6000"` in `Program.cs`
  - [ ] 1.5 Replace hardcoded `"00000000-0000-0000-0000-000000000002"` in the `x-user-id` metadata header with `userId` in `Program.cs`
  - [ ] 1.6 Remove the default value `"SHP-2026-00128"` from `shipmentId` — initialize to `null` or empty string instead in `Program.cs`
  - [ ] 1.7 Fix the silent catch block: replace the `// In demo offline mode...` comment with `Console.WriteLine($"[WARN] IngestPosition failed: {ex.Message}");` in `Program.cs`
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 5.2, 6.1, 6.2, 6.3, 6.4, 4.2_

- [ ] 2. Validate `--shipment` UUID
  - After the CLI arg parsing loop, add a guard in `Program.cs`:
    - If `shipmentId` is null/empty or `!Guid.TryParse(shipmentId, out _)`, print the following and `return`:
      ```
      Usage: gps-simulator --shipment <uuid> [--interval <sec>] [--speed <kmh>] [--grpc <url>] [--tenant <id>]
      Error: --shipment is required and must be a valid UUID.
      ```
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 1.1, 1.6, 2.4, 5.3_

- [ ] 3. Add GetShipment — real shipment validation via gRPC
  - [ ] 3.1 In `gps-simulator.csproj`, add a second `<Protobuf>` entry:
    ```xml
    <Protobuf Include="../../protos/shipment_workflow.proto" GrpcServices="Client" Link="Protos/shipment_workflow.proto" />
    ```
  - [ ] 3.2 In `Program.cs`, add `using ShipmentWorkflow.Grpc;` at the top
  - [ ] 3.3 After UUID validation, before printing route info, insert the shipment validation block:
    - Create a gRPC channel to `shipmentGrpcUrl`
    - Build headers with `x-tenant-id` and `x-user-id`
    - Call `GetShipment(new GetShipmentRequest { Id = shipmentId })` with those headers
    - On success: print `Shipment found: {response.CustomerName} ({response.Status})`
    - On `RpcException` with `StatusCode.NotFound`: print `Error: Shipment '{shipmentId}' not found.` and `return`
    - On any other exception: print `Error: Cannot connect to Shipment Workflow Service at {shipmentGrpcUrl}: {ex.Message}` and `return`
  - Files: `tools/gps-simulator/gps-simulator.csproj`, `tools/gps-simulator/Program.cs`
  - _Requirements: 2.1, 2.2, 2.3, 2.5, 5.1_

- [ ] 4. VERIFY vehicleId mapping (documentation + comment)
  - No code logic changes. Add the following comment block in `Program.cs` immediately above the simulation loop:
    ```csharp
    // vehicle_id = shipmentId (UUID string)
    // GPS Tracking links positions to a shipment via VehicleShipmentAssignment,
    // which is created automatically when a route is assigned to the shipment.
    // BFF query: GET /api/v1/tracking/{shipmentId}/current?type=vehicle (always works)
    //            GET /api/v1/tracking/{shipmentId}/current?type=shipment (requires route assignment)
    ```
  - Confirm: `IngestPositionRequest.VehicleId` is already set to `shipmentId` — no field change needed
  - Confirm: `IngestPositionRequest.DeviceId` is already `$"sim-dev-{shipmentId}"` — no change needed
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 4.4, 8.4, 8.5_

- [ ] 5. Improve startup banner and route info output
  - Replace the existing `Console.WriteLine("Aurora GPS Simulator")` and `Console.WriteLine($"Shipment: {shipmentId}")` calls with the full banner format:
    ```
    Aurora GPS Simulator
    Shipment : {shipmentId}
    Interval : {intervalSeconds}s  Speed: {baseSpeed} km/h
    GPS URL  : {grpcUrl}
    Tenant   : {tenantId}
    ```
  - Add a blank line after the banner (before shipment validation)
  - The `Route points: {routePoints.Count}` line is already present — keep it
  - Add `Route: San José CR → Panama City PA (hardcoded ROAD corridor)` printed after building `routePoints`, before printing the count
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 3.1, 3.2, 7.4_

- [ ] 6. Verify IngestPosition telemetry loop field mapping
  - Verify the following field assignments in the existing `IngestPositionRequest` builder (no proto changes needed — this is a code review + minor fix task):
    - `ExternalReadingId = $"sim-{Guid.NewGuid():N}"` — confirm present
    - `DeviceId = $"sim-dev-{shipmentId}"` — confirm present
    - `VehicleId = shipmentId` — confirm present
    - `SpeedKph`, `HeadingDegrees`, `AccuracyMeters = 3.5` — confirm present
    - `RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)` — confirm present
  - If the progress line `[{i+1}/{routePoints.Count}] {lat:F4},{lng:F4} | {speed:F0} km/h` is not already present after the `IngestPositionAsync` call, add it — confirm it is already there
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 7.1, 7.2, 8.1, 8.2, 8.3, 8.4, 8.5_

- [ ] 7. Add Ctrl+C graceful cancellation
  - Before the simulation loop in `Program.cs`:
    - Add `using var cts = new CancellationTokenSource();`
    - Register: `Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };`
  - In the loop, replace `await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));` with:
    ```csharp
    try { await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token); }
    catch (OperationCanceledException) { Console.WriteLine("\nSimulation cancelled."); break; }
    ```
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 5.5_

- [ ] 8. Track success/failure counts
  - Before the simulation loop in `Program.cs`, add:
    ```csharp
    int successCount = 0;
    int failCount = 0;
    ```
  - In the `try` block, after `await client.IngestPositionAsync(...)` succeeds: add `successCount++;`
  - In the `catch` block, after printing the `[WARN]` message: add `failCount++;`
  - Replace the final completion message with:
    ```csharp
    Console.WriteLine($"\nSimulation completed. {successCount} sent, {failCount} failed.");
    ```
  - File: `tools/gps-simulator/Program.cs`
  - _Requirements: 7.5_

- [ ] 9. Build and smoke-test verification
  - Run `dotnet build tools/gps-simulator` and confirm: zero errors, zero warnings
  - Run `dotnet run --project tools/gps-simulator` (no args) and confirm: usage message prints and process exits with a non-zero code
  - Run `dotnet run --project tools/gps-simulator -- --shipment not-a-uuid` and confirm: UUID validation error message prints
  - Files: `tools/gps-simulator/Program.cs`, `tools/gps-simulator/gps-simulator.csproj`

- [ ] 10. Manual E2E verification with frontend (no code changes)
  - Prerequisites:
    1. All Aurora services running: GPS Tracking on `:6002`, Shipment Workflow on `:6000`, BFF
    2. A shipment exists with an assigned route (so `VehicleShipmentAssignment` record exists in GPS Tracking DB)
    3. Frontend open on the Shipment Tracking page for that shipment
  - Steps:
    1. Get the shipment UUID from `GET /api/v1/shipments` or the FE shipment list
    2. Run: `dotnet run --project tools/gps-simulator -- --shipment <uuid>`
    3. Observe console: "Shipment found: ..." line, then `[1/181]`, `[2/181]` ...
    4. In the FE: verify the map marker moves along the San José → Panama City corridor
    5. Verify `GET /api/v1/tracking/<uuid>/current?type=vehicle` returns an updated lat/lng
    6. Verify `GET /api/v1/tracking/<uuid>/history?type=vehicle&from=...&to=...` returns a growing list of positions
    7. Press Ctrl+C and verify "Simulation cancelled." message appears and the process exits cleanly
  - Success criteria: marker moves, history grows, Ctrl+C exits cleanly

## Notes

- Tasks 1–8 all modify `tools/gps-simulator/Program.cs`; task 3 also modifies `gps-simulator.csproj`
- Tasks 4 and 6 are verification tasks with minimal or no logic changes
- Tasks 9 and 10 are purely verification — no code is written
- No optional tasks: all tasks are required for the demo to work correctly
- No property-based testing tasks in this plan

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["1.5", "1.6", "1.7"] },
    { "id": 2, "tasks": ["2"] },
    { "id": 3, "tasks": ["3.1", "3.2"] },
    { "id": 4, "tasks": ["3.3", "4", "5", "6"] },
    { "id": 5, "tasks": ["7", "8"] },
    { "id": 6, "tasks": ["9"] },
    { "id": 7, "tasks": ["10"] }
  ]
}
```
