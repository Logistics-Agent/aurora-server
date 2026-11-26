namespace GpsSimulator.Tests;

public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Running GpsSimulator Unit Tests...");
        int passed = 0;
        int failed = 0;

        void Run(string name, Action action)
        {
            try
            {
                action();
                Console.WriteLine($"  [PASS] {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
                Console.ResetColor();
                failed++;
            }
        }

        var configTests = new SimulationConfigTests();
        Run("SimulationConfig: Backward compatible single shipment sets single mode", () => configTests.Parse_BackwardCompatibleSingleShipment_SetsSingleMode());
        Run("SimulationConfig: Fleet mode with vehicles and behavior parses correctly", () => configTests.Parse_FleetModeWithVehiclesAndBehavior_ParsesCorrectly());
        Run("SimulationConfig: Profile shortcut configures fleet correctly", () => configTests.Parse_ProfileShortcut_ConfiguresFleetCorrectly());
        Run("SimulationConfig: Scenario mode parses scenario name", () => configTests.Parse_ScenarioMode_ParsesScenarioName());
        Run("SimulationConfig: Invalid mode throws ArgumentException", () => configTests.Parse_InvalidMode_ThrowsArgumentException());
        Run("SimulationConfig: Single mode without shipment throws ArgumentException", () => configTests.Parse_SingleModeWithoutShipment_ThrowsArgumentException());

        var routeTests = new RouteTrajectoryTests();
        Run("RouteTrajectory: FromWaypoints interpolates correct number of points", () => routeTests.FromWaypoints_InterpolatesCorrectNumberOfPoints());
        Run("RouteTrajectory: CalculateHeading calculations are accurate", () => routeTests.CalculateHeading_HeadingCalculationsAreAccurate());
        Run("RouteTrajectory: Haversine distance returns realistic distance", () => routeTests.HaversineDistance_ReturnsRealisticDistance());

        var telemetryTests = new TelemetryGeneratorTests();
        Run("TelemetryGenerator: Increments sequence and advances waypoints", () => telemetryTests.GenerateNext_IncrementsSequenceAndAdvancesWaypoints());
        Run("TelemetryGenerator: Deviation behavior applies offset", () => telemetryTests.GenerateNext_DeviationBehavior_AppliesOffset());

        var metricsTests = new SimulationMetricsTests();
        Run("SimulationMetrics: Calculates accurate percentiles (p50, p95, p99)", () => metricsTests.Metrics_CalculatesAccuratePercentiles());
        Run("SimulationMetrics: Tracks failures and timeouts", () => metricsTests.Metrics_TracksFailuresAndTimeouts());

        Console.WriteLine();
        Console.WriteLine($"Test Run Completed: {passed} Passed, {failed} Failed.");
        return failed == 0 ? 0 : 1;
    }
}

