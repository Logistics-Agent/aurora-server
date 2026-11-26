using GpsSimulator.Models;

namespace GpsSimulator.Tests;

public class SimulationConfigTests
{
    public void Parse_BackwardCompatibleSingleShipment_SetsSingleMode()
    {
        var uuid = Guid.NewGuid().ToString();
        string[] args = ["--shipment", uuid, "--interval", "5", "--speed", "65", "--grpc", "http://custom:6002"];

        var config = SimulationConfig.Parse(args);

        if (config.Mode != SimulationMode.Single) throw new Exception("Expected Single mode");
        if (config.SingleShipmentId != uuid) throw new Exception("Expected shipment ID match");
        if (config.IntervalSeconds != 5.0) throw new Exception("Expected interval 5.0");
        if (config.BaseSpeedKph != 65.0) throw new Exception("Expected speed 65.0");
        if (config.GpsGrpcUrl != "http://custom:6002") throw new Exception("Expected custom GPS URL");
    }

    public void Parse_FleetModeWithVehiclesAndBehavior_ParsesCorrectly()
    {
        string[] args =
        [
            "--mode", "fleet",
            "--vehicles", "100",
            "--interval", "1",
            "--duration", "300",
            "--seed", "42",
            "--behavior", "mixed",
            "--auto-assign"
        ];

        var config = SimulationConfig.Parse(args);

        if (config.Mode != SimulationMode.Fleet) throw new Exception("Expected Fleet mode");
        if (config.VehiclesLimit != 100) throw new Exception("Expected 100 vehicles");
        if (config.IntervalSeconds != 1.0) throw new Exception("Expected interval 1.0");
        if (config.DurationSeconds != 300) throw new Exception("Expected duration 300");
        if (config.RandomSeed != 42) throw new Exception("Expected seed 42");
        if (config.Behavior != SimulationBehavior.Mixed) throw new Exception("Expected Mixed behavior");
        if (!config.AutoAssignVehicle) throw new Exception("Expected AutoAssignVehicle true");
    }

    public void Parse_InvalidMode_ThrowsArgumentException()
    {
        string[] args = ["--mode", "invalid_mode"];
        try
        {
            SimulationConfig.Parse(args);
            throw new Exception("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    public void Parse_SingleModeWithoutShipment_ThrowsArgumentException()
    {
        string[] args = ["--mode", "single"];
        try
        {
            SimulationConfig.Parse(args);
            throw new Exception("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    public void Parse_ProfileShortcut_ConfiguresFleetCorrectly()
    {
        string[] args = ["--profile", "C", "--duration", "60", "--seed", "42"];
        var config = SimulationConfig.Parse(args);

        if (config.Mode != SimulationMode.Fleet) throw new Exception("Expected Fleet mode");
        if (config.Profile != SimulationProfile.C) throw new Exception("Expected Profile C");
        if (config.VehiclesLimit != 100) throw new Exception("Expected 100 vehicles");
        if (config.IntervalSeconds != 1.0) throw new Exception("Expected interval 1.0");
        if (config.DurationSeconds != 60) throw new Exception("Expected duration 60");
        if (config.RandomSeed != 42) throw new Exception("Expected seed 42");
    }

    public void Parse_ScenarioMode_ParsesScenarioName()
    {
        string[] args = ["--mode", "scenario", "--scenario", "geofence"];
        var config = SimulationConfig.Parse(args);

        if (config.Mode != SimulationMode.Scenario) throw new Exception("Expected Scenario mode");
        if (config.ScenarioName != "geofence") throw new Exception("Expected geofence scenario");
    }
}
