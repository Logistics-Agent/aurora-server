# Implementation Plan: GPS Simulator Enhancements

## Overview

Enhance the existing `tools/gps-simulator/Program.cs` to fix two bugs, add environment variable support for all configurable values, add a structured startup banner, add UUID validation for `--shipment`, add optional shipment validation via the Shipment Workflow Service, add graceful `Ctrl+C` cancellation, and print a proper completion message. No new architecture — all logic stays in a single `Program.cs` file. The test project (if added) lives alongside as a sibling `xUnit` project.

## Tasks

- [ ] 1. Fix default environment variable values and silent exception
  - [ ] 1.1 Fix `GPS_GRPC_URL` default from `localhost:5004` to `http://localhost:6002`
    - Change the fallback value in the env-var read: `Environment.GetEnvironmentVariable("GPS_GRPC_URL") ?? "http://localhost:6002"`
    - _Requirements: 6.1, 6.4_

  - [ ] 1.2 Fix `TENANT_ID` default to `01920000-0000-7000-8000-000000000001`
    - Change the fallback value in the env-var read for `TENANT_ID`
    - _Requirements: 6.3, 6.4_

  - [ ] 1.3 Fix the silent exception catch in `IngestPositionAsync`
    - Replace the empty `catch` block with: `Console.WriteLine($"[WARN] IngestPosition failed: {ex.Message}");`
    - The loop must continue after printing the warning
    - _Requirements: 5.2_

- [ ] 2. Add remaining environment variable support and `USER_ID` header
  - [ ] 2.1 Add `SHIPMENT_GRPC_URL` env var (default `http://localhost:6000`)
    - Read `Environment.GetEnvironmentVariable("SHIPMENT_GRPC_URL") ?? "http://localhost:6000"` into a `shipmentGrpcUrl` variable
    - _Requirements: 6.2, 6.4_

  - [ ] 2.2 Add `USER_ID` env var (default `01910000-0000-7000-8000-000000000001`)
    - Read `Environment.GetEnvironmentVariable("USER_ID") ?? "01910000-0000-7000-8000-000000000001"` into a `userId` variable
    - Replace the hardcoded `"00000000-0000-0000-0000-000000000002"` value in the gRPC metadata with `userId`
    - _Requirements: 4.2_

- [ ] 3. Add `--shipment` UUID format validation and `Ctrl+C` cancellation
  - [ ] 3.1 Add UUID validation for `--shipment` argument
    - After parsing CLI args, call `Guid.TryParse(shipmentId, out _)` if a `--shipment` value was provided
    - If parsing fails or no `--shipment` is provided, print usage message and `return`
    - Usage message: `"Usage: gps-simulator --shipment <uuid> [--interval <sec>] [--speed <kmh>] [--grpc <url>] [--tenant <id>]"`
    - _Requirements: 1.1, 1.6, 2.4, 5.3_

  - [ ]* 3.2 Write property test for UUID validation (Property 2)
    - **Property 2: UUID validation correctly classifies inputs**
    - Test that the validator returns `true` iff `Guid.TryParse` succeeds on any arbitrary string input
    - Use FsCheck or xUnit with `[Theory]` data to exercise valid GUIDs, invalid strings, empty string, null-like inputs
    - Annotate with `// Feature: gps-simulator, Property 2`
    - **Validates: Requirements 2.4, 5.3**

  - [ ] 3.3 Add graceful `Ctrl+C` cancellation via `CancellationToken`
    - Create a `CancellationTokenSource cts = new()` in `Main`
    - Register `Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };`
    - Pass `cts.Token` to `Task.Delay` inside the simulation loop
    - Wrap the delay in a `try/catch (OperationCanceledException)` to exit the loop cleanly
    - _Requirements: 5.5_

- [ ] 4. Add structured startup banner
  - [ ] 4.1 Print startup banner with all configuration values
    - Replace the two existing `Console.WriteLine` calls with the structured banner format:
      ```
      Aurora GPS Simulator
      Shipment : {shipmentId}
      Interval : {intervalSeconds}s  Speed: {baseSpeed} km/h
      GPS URL  : {grpcUrl}
      Tenant   : {tenantId}
      ```
    - Print the blank line separator after the banner
    - _Requirements: 7.1, 6.5_

- [ ] 5. Add `shipment_workflow.proto` to csproj and implement shipment validation
  - [ ] 5.1 Add `shipment_workflow.proto` as a gRPC client in `gps-simulator.csproj`
    - Add inside the existing `<ItemGroup>` with Protobuf entries:
      ```xml
      <Protobuf Include="../../protos/shipment_workflow.proto"
                GrpcServices="Client"
                Link="Protos/shipment_workflow.proto" />
      ```
    - Verify the project compiles with the new proto (run `dotnet build`)
    - _Requirements: 2.1_

  - [ ] 5.2 Implement optional shipment validation using `GetShipment`
    - Add `using ShipmentWorkflow.Grpc;` at the top of `Program.cs`
    - After printing the banner, create a gRPC channel to `shipmentGrpcUrl` and a `ShipmentWorkflowServiceClient`
    - Call `GetShipment(new GetShipmentRequest { Id = shipmentId })` with the same tenant/user headers
    - On success: print `"Shipment found: {customerName} ({status})"` and continue
    - On `RpcException` with `StatusCode.NotFound`: print `$"Error: Shipment '{shipmentId}' not found."` and `return`
    - On any other exception: print `$"Error: Cannot connect to Shipment Workflow Service at {shipmentGrpcUrl}: {ex.Message}"` and `return`
    - _Requirements: 2.1, 2.2, 2.3, 2.5, 5.1_

  - [ ]* 5.3 Write unit tests for shipment validation error paths
    - Test NotFound path exits with correct message
    - Test unreachable service path exits with correct message
    - Test success path prints customer name and status
    - _Requirements: 2.2, 2.3, 5.1_

- [ ] 6. Fix completion message and add points-sent counter
  - [ ] 6.1 Track the number of positions successfully considered and print completion message
    - Add an `int sentCount = 0;` counter before the loop; increment after each `Console.WriteLine` progress line (count all points attempted, warnings don't skip the count)
    - Replace the existing `"ROAD leg destination reached. Simulation completed."` line with:
      `Console.WriteLine($"\nSimulation completed. {sentCount} points sent.");`
    - _Requirements: 7.5_

- [ ] 7. Checkpoint — Build and smoke-test
  - Run `dotnet build tools/gps-simulator` and verify it compiles without errors or warnings
  - Run the simulator without arguments and confirm the usage message is printed and the process exits
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Add property-based and unit tests for pure logic functions
  - [ ] 8.1 Set up xUnit test project alongside the simulator
    - Create `tools/gps-simulator-tests/gps-simulator-tests.csproj` referencing xUnit and FsCheck (or FsCheck.Xunit)
    - Add a project reference to `gps-simulator` (make `Program` and helpers `internal` or refactor into testable static methods)
    - _Requirements: (testing infrastructure)_

  - [ ]* 8.2 Write property test for interval and speed bounds enforcement (Property 1)
    - **Property 1: Interval and speed bounds are enforced**
    - For any integer `n`, parsed interval = `max(1, n)`. For any double `d`, parsed speed = `max(10.0, d)`.
    - Annotate with `// Feature: gps-simulator, Property 1`
    - **Validates: Requirements 1.2, 1.3**

  - [ ]* 8.3 Write property test for route interpolation point count (Property 3)
    - **Property 3: Route interpolation produces the expected point count**
    - For any pair of waypoints and step count `N ≥ 1`, `InterpolateRoute` with 1 segment and N steps produces exactly `N + 1` total points (N intermediates + final endpoint)
    - All produced points must lie on the straight-line segment between the two input endpoints
    - Annotate with `// Feature: gps-simulator, Property 3`
    - **Validates: Requirements 3.2**

  - [ ]* 8.4 Write property test for heading validity (Property 4)
    - **Property 4: Heading is always a valid compass bearing**
    - For any two distinct coordinate pairs, `CalculateHeading` returns a value in `[0.0, 360.0)`
    - Annotate with `// Feature: gps-simulator, Property 4`
    - **Validates: Requirements 3.3**

  - [ ]* 8.5 Write property test for speed variation bounds (Property 5)
    - **Property 5: Speed variation stays within ±3 km/h**
    - For any base speed value, the simulated speed satisfies `|appliedSpeed - baseSpeed| ≤ 3.0`
    - Annotate with `// Feature: gps-simulator, Property 5`
    - **Validates: Requirements 3.4**

  - [ ]* 8.6 Write property test for unique external reading IDs (Property 6)
    - **Property 6: External reading IDs are unique across a simulation run**
    - For any `N ≥ 1` generated `IngestPositionRequest` objects, all `ExternalReadingId` values are distinct and each matches `^sim-[0-9a-f]{32}$`
    - Annotate with `// Feature: gps-simulator, Property 6`
    - **Validates: Requirements 3.7, 8.3**

  - [ ]* 8.7 Write property test for request field mapping completeness (Property 7)
    - **Property 7: Request field mapping is complete and consistent**
    - For any shipment UUID and route point, the built `IngestPositionRequest` satisfies: `VehicleId == shipmentId`, `DeviceId == "sim-dev-" + shipmentId`, coordinates match route point, `AccuracyMeters == 3.5`, `RecordedAt` is valid UTC, `SpeedKph` and `HeadingDegrees` are populated
    - Annotate with `// Feature: gps-simulator, Property 7`
    - **Validates: Requirements 4.3, 4.4, 4.5, 8.1**

  - [ ]* 8.8 Write property test for coordinate range validation (Property 8)
    - **Property 8: Geographic coordinates are within valid ranges**
    - For any latitude outside `[-90, 90]` or longitude outside `[-180, 180]`, the coordinate validator rejects the value; valid ranges are accepted
    - Annotate with `// Feature: gps-simulator, Property 8`
    - **Validates: Requirements 8.6**

  - [ ]* 8.9 Write unit tests for CLI argument parsing
    - Test: defaults applied when no args given
    - Test: `--interval` clamped to minimum 1
    - Test: `--speed` clamped to minimum 10.0
    - Test: `--grpc` overrides env var
    - Test: `--tenant` overrides env var
    - _Requirements: 1.1–1.5_

- [ ] 9. Final checkpoint — All tests pass
  - Run `dotnet test tools/gps-simulator-tests` (if test project was created)
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP; the simulator is fully functional without the test project
- Tasks 1–6 are purely modifications to `Program.cs` and `gps-simulator.csproj` — no new files outside those two
- Task 5.1 must complete before Task 5.2 (proto code generation required)
- Task 3.1 (UUID validation) must come before Task 5.2 (shipment validation) — the UUID guard should fire first
- The cancellation token (Task 3.3) wraps `Task.Delay` only; gRPC calls do not need the token for this demo tool
- All env vars are read once at startup; CLI args parsed in the existing loop can override them where applicable (`--grpc` overrides `GPS_GRPC_URL`, `--tenant` overrides `TENANT_ID`)
- The design specifies no `--shipment-grpc` CLI override; `SHIPMENT_GRPC_URL` is env-var only

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "3.3", "4.1"] },
    { "id": 3, "tasks": ["3.2", "5.1", "6.1"] },
    { "id": 4, "tasks": ["5.2", "8.1"] },
    { "id": 5, "tasks": ["5.3", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7", "8.8", "8.9"] }
  ]
}
```
