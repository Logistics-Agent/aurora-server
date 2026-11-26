namespace GpsSimulator.Models;

public enum SimulationMode
{
    Single,
    Auto,
    Fleet,
    Scenario
}

public enum SimulationProfile
{
    None,
    A,
    B,
    C,
    D,
    E
}

public enum SimulationBehavior
{
    Normal,
    Mixed,
    Deviation,
    SignalLoss
}

public class SimulationConfig
{
    public SimulationMode Mode { get; set; } = SimulationMode.Auto;
    public SimulationProfile Profile { get; set; } = SimulationProfile.None;
    public string? ScenarioName { get; set; } = "all";
    public string? SingleShipmentId { get; set; }
    public string? ShipmentsFilePath { get; set; }
    public int? VehiclesLimit { get; set; }
    public double IntervalSeconds { get; set; } = 3.0;
    public double BaseSpeedKph { get; set; } = 50.0;
    public SimulationBehavior Behavior { get; set; } = SimulationBehavior.Normal;
    public int? DurationSeconds { get; set; }
    public int? RandomSeed { get; set; }
    public string GpsGrpcUrl { get; set; } = "http://localhost:6002";
    public string ShipmentGrpcUrl { get; set; } = "http://localhost:6000";
    public string RouteGrpcUrl { get; set; } = "http://localhost:5002";
    public string TenantId { get; set; } = "01920000-0000-7000-8000-000000000001";
    public string UserId { get; set; } = "01910000-0000-7000-8000-000000000001";
    public bool AutoAssignVehicle { get; set; } = false;
    public int MaxConcurrency { get; set; } = 1000;

    public const string Usage = """
        Aurora GPS Simulator
        Usage:
          gps-simulator --shipment <uuid> [options]
          gps-simulator --mode auto [options]
          gps-simulator --mode fleet --vehicles <count> [options]
          gps-simulator --mode fleet --shipments <path-to-json> [options]
          gps-simulator --profile <A|B|C|D|E> [options]
          gps-simulator --mode scenario [--scenario <all|normal|duplicate|late|geofence|stop|multitenant>]

        Profiles:
          Profile A: 10 vehicles x 1 event/5s (2 events/sec)
          Profile B: 50 vehicles x 1 event/3s (16.7 events/sec)
          Profile C: 100 vehicles x 1 event/1s (100 events/sec)
          Profile D: 500 vehicles x 1 event/1s (500 events/sec)
          Profile E: 1,000 vehicles x 1 event/5s (200 events/sec)

        Options:
          --mode <single|auto|fleet|scenario> Simulation mode (default: single if --shipment set, else auto)
          --profile <A|B|C|D|E>         Load profile shortcut (overrides vehicles & interval)
          --scenario <name>             Target scenario for scenario mode: all | normal | duplicate | late | geofence | stop | multitenant
          --shipment <uuid>             Target single shipment UUID
          --shipments <file>            Path to JSON file containing shipment IDs
          --vehicles <number>           Maximum virtual vehicles/shipments to simulate
          --interval <seconds>          Interval between GPS telemetry events (default: 3)
          --speed <kmh>                 Base vehicle speed in km/h (default: 50)
          --behavior <behavior>         Behavior profile: normal | mixed | deviation | signal-loss (default: normal)
          --duration <seconds>          Total duration to run simulation (default: run until route end)
          --seed <number>               Deterministic random seed
          --gps, --grpc <url>           GPS Tracking gRPC URL (default: http://localhost:6002 or $GPS_GRPC_URL)
          --shipment-grpc <url>         Shipment Workflow gRPC URL (default: http://localhost:6000 or $SHIPMENT_GRPC_URL)
          --route-grpc <url>            Route Planning gRPC URL (default: http://localhost:5002 or $ROUTE_GRPC_URL)
          --tenant <id>                 Tenant ID (default: 01920000-0000-7000-8000-000000000001 or $TENANT_ID)
          --user-id <id>                User ID (default: 01910000-0000-7000-8000-000000000001 or $USER_ID)
          --auto-assign                 Auto-assign vehicle ID to shipment if unassigned
          --concurrency <number>        Max concurrent active vehicle tasks (default: 1000)
          -h, --help                    Show this help message
        """;

    public static SimulationConfig Parse(string[] args)
    {
        var config = new SimulationConfig
        {
            GpsGrpcUrl = Environment.GetEnvironmentVariable("GPS_GRPC_URL") ?? "http://localhost:6002",
            ShipmentGrpcUrl = Environment.GetEnvironmentVariable("SHIPMENT_GRPC_URL") ?? "http://localhost:6000",
            RouteGrpcUrl = Environment.GetEnvironmentVariable("ROUTE_GRPC_URL") ?? "http://localhost:5002",
            TenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? "01920000-0000-7000-8000-000000000001",
            UserId = Environment.GetEnvironmentVariable("USER_ID") ?? "01910000-0000-7000-8000-000000000001"
        };

        bool modeExplicitlySet = false;

        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option is "--help" or "-h")
            {
                Console.WriteLine(Usage);
                Environment.Exit(0);
            }

            if (option is "--auto-assign")
            {
                config.AutoAssignVehicle = true;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for '{option}'.");
            }

            string value = args[++i];
            switch (option)
            {
                case "--mode":
                    config.Mode = Enum.Parse<SimulationMode>(value, ignoreCase: true);
                    modeExplicitlySet = true;
                    break;
                case "--profile":
                    var profile = Enum.Parse<SimulationProfile>(value, ignoreCase: true);
                    config.Profile = profile;
                    config.Mode = SimulationMode.Fleet;
                    modeExplicitlySet = true;
                    switch (profile)
                    {
                        case SimulationProfile.A:
                            config.VehiclesLimit = 10;
                            config.IntervalSeconds = 5.0;
                            break;
                        case SimulationProfile.B:
                            config.VehiclesLimit = 50;
                            config.IntervalSeconds = 3.0;
                            break;
                        case SimulationProfile.C:
                            config.VehiclesLimit = 100;
                            config.IntervalSeconds = 1.0;
                            break;
                        case SimulationProfile.D:
                            config.VehiclesLimit = 500;
                            config.IntervalSeconds = 1.0;
                            break;
                        case SimulationProfile.E:
                            config.VehiclesLimit = 1000;
                            config.IntervalSeconds = 5.0;
                            break;
                        default:
                            throw new ArgumentException($"Unknown profile '{value}'. Allowed: A, B, C, D, E.");
                    }
                    break;
                case "--scenario":
                    config.ScenarioName = value.ToLowerInvariant();
                    if (!modeExplicitlySet)
                    {
                        config.Mode = SimulationMode.Scenario;
                    }
                    break;
                case "--shipment":
                    config.SingleShipmentId = value;
                    if (!modeExplicitlySet)
                    {
                        config.Mode = SimulationMode.Single;
                    }
                    break;
                case "--shipments":
                    config.ShipmentsFilePath = value;
                    if (!modeExplicitlySet)
                    {
                        config.Mode = SimulationMode.Fleet;
                    }
                    break;
                case "--vehicles" when int.TryParse(value, out var v) && v > 0:
                    config.VehiclesLimit = v;
                    if (!modeExplicitlySet)
                    {
                        config.Mode = SimulationMode.Fleet;
                    }
                    break;
                case "--interval" when double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsedInterval):
                    config.IntervalSeconds = Math.Max(0.1, parsedInterval);
                    break;
                case "--speed" when double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsedSpeed):
                    config.BaseSpeedKph = Math.Max(1.0, parsedSpeed);
                    break;
                case "--behavior":
                    config.Behavior = value.ToLowerInvariant() switch
                    {
                        "normal" => SimulationBehavior.Normal,
                        "mixed" => SimulationBehavior.Mixed,
                        "deviation" => SimulationBehavior.Deviation,
                        "signal-loss" or "signalloss" => SimulationBehavior.SignalLoss,
                        _ => throw new ArgumentException($"Unknown behavior '{value}'. Allowed: normal, mixed, deviation, signal-loss.")
                    };
                    break;
                case "--duration" when int.TryParse(value, out var d) && d > 0:
                    config.DurationSeconds = d;
                    break;
                case "--seed" when int.TryParse(value, out var s):
                    config.RandomSeed = s;
                    break;
                case "--gps" or "--grpc":
                    config.GpsGrpcUrl = value;
                    break;
                case "--shipment-grpc":
                    config.ShipmentGrpcUrl = value;
                    break;
                case "--route-grpc":
                    config.RouteGrpcUrl = value;
                    break;
                case "--tenant":
                    config.TenantId = value;
                    break;
                case "--user-id":
                    config.UserId = value;
                    break;
                case "--concurrency" when int.TryParse(value, out var c) && c > 0:
                    config.MaxConcurrency = c;
                    break;
                default:
                    throw new ArgumentException($"Invalid option or parameter '{option}'.");
            }
        }

        if (config.Mode == SimulationMode.Single)
        {
            if (string.IsNullOrWhiteSpace(config.SingleShipmentId) || !Guid.TryParse(config.SingleShipmentId, out _))
            {
                throw new ArgumentException("--shipment is required and must be a valid UUID when in single mode.");
            }
        }

        return config;
    }
}

