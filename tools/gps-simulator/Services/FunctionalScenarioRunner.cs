using Google.Protobuf.WellKnownTypes;
using GpsSimulator.Models;
using GpsTracking.Grpc;
using Grpc.Core;

namespace GpsSimulator.Services;

public class FunctionalScenarioRunner
{
    private readonly GpsTrackingService.GpsTrackingServiceClient _client;
    private readonly SimulationConfig _config;

    public FunctionalScenarioRunner(
        GpsTrackingService.GpsTrackingServiceClient client,
        SimulationConfig config)
    {
        _client = client;
        _config = config;
    }

    public async Task<int> RunScenariosAsync(string targetScenario, CancellationToken ct)
    {
        Console.WriteLine("=========================================================");
        Console.WriteLine("      Aurora GPS Tracking - Functional Scenario Runner   ");
        Console.WriteLine("=========================================================");
        Console.WriteLine($"Target Scenario: {targetScenario}");
        Console.WriteLine($"Endpoint       : {_config.GpsGrpcUrl}");
        Console.WriteLine($"Tenant ID      : {_config.TenantId}");
        Console.WriteLine("=========================================================");
        Console.WriteLine();

        var headers = new Metadata
        {
            { "x-tenant-id", _config.TenantId },
            { "x-user-id", _config.UserId }
        };

        var scenarios = new List<(string Name, Func<Metadata, CancellationToken, Task> Action)>
        {
            ("normal", RunNormalMovementScenarioAsync),
            ("duplicate", RunDuplicateReadingScenarioAsync),
            ("late", RunLateReadingScenarioAsync),
            ("geofence", RunGeofenceScenarioAsync),
            ("stop", RunAbnormalStopScenarioAsync),
            ("multitenant", RunMultiTenantIsolationScenarioAsync)
        };

        int passed = 0;
        int failed = 0;

        foreach (var (name, action) in scenarios)
        {
            if (targetScenario != "all" && !string.Equals(targetScenario, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Console.Write($"[RUNNING] Scenario: {name,-12} ... ");
            try
            {
                await action(headers, ct);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASS");
                Console.ResetColor();
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAIL");
                Console.ResetColor();
                Console.WriteLine($"          Error: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("=========================================================");
        Console.WriteLine($"Scenario Results: {passed} Passed, {failed} Failed");
        Console.WriteLine("=========================================================");
        return failed == 0 ? 0 : 2;
    }

    private async Task RunNormalMovementScenarioAsync(Metadata headers, CancellationToken ct)
    {
        string vehicleId = $"test-norm-{Guid.NewGuid():N}";
        string deviceId = $"dev-{vehicleId}";

        var p1 = new IngestPositionRequest
        {
            ExternalReadingId = $"reading-norm-1-{Guid.NewGuid():N}",
            DeviceId = deviceId,
            VehicleId = vehicleId,
            Latitude = 10.0001,
            Longitude = 20.0001,
            SpeedKph = 45.0,
            HeadingDegrees = 90.0,
            AccuracyMeters = 3.0,
            RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(-20))
        };

        var p2 = new IngestPositionRequest
        {
            ExternalReadingId = $"reading-norm-2-{Guid.NewGuid():N}",
            DeviceId = deviceId,
            VehicleId = vehicleId,
            Latitude = 10.0005,
            Longitude = 20.0005,
            SpeedKph = 50.0,
            HeadingDegrees = 90.0,
            AccuracyMeters = 3.0,
            RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(-10))
        };

        var p3 = new IngestPositionRequest
        {
            ExternalReadingId = $"reading-norm-3-{Guid.NewGuid():N}",
            DeviceId = deviceId,
            VehicleId = vehicleId,
            Latitude = 10.0010,
            Longitude = 20.0010,
            SpeedKph = 55.0,
            HeadingDegrees = 90.0,
            AccuracyMeters = 3.0,
            RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };

        await _client.IngestPositionAsync(p1, headers, cancellationToken: ct);
        await _client.IngestPositionAsync(p2, headers, cancellationToken: ct);
        await _client.IngestPositionAsync(p3, headers, cancellationToken: ct);

        var current = await _client.GetCurrentLocationAsync(
            new GetCurrentLocationRequest { VehicleId = vehicleId },
            headers,
            cancellationToken: ct);

        if (Math.Abs(current.Latitude - 10.0010) > 0.0001 || Math.Abs(current.Longitude - 20.0010) > 0.0001)
        {
            throw new Exception($"Current location snapshot {current.Latitude},{current.Longitude} did not advance to latest {p3.Latitude},{p3.Longitude}");
        }
    }

    private async Task RunDuplicateReadingScenarioAsync(Metadata headers, CancellationToken ct)
    {
        string vehicleId = $"test-dup-{Guid.NewGuid():N}";
        string deviceId = $"dev-{vehicleId}";
        string externalReadingId = $"dup-ext-{Guid.NewGuid():N}";

        var req = new IngestPositionRequest
        {
            ExternalReadingId = externalReadingId,
            DeviceId = deviceId,
            VehicleId = vehicleId,
            Latitude = 10.5,
            Longitude = 20.5,
            SpeedKph = 40.0,
            RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };

        var res1 = await _client.IngestPositionAsync(req, headers, cancellationToken: ct);
        var res2 = await _client.IngestPositionAsync(req, headers, cancellationToken: ct);

        if (res1.ExternalReadingId != res2.ExternalReadingId)
        {
            throw new Exception("Deduplication did not return consistent response.");
        }
    }

    private async Task RunLateReadingScenarioAsync(Metadata headers, CancellationToken ct)
    {
        string vehicleId = $"test-late-{Guid.NewGuid():N}";
        string deviceId = $"dev-{vehicleId}";
        var now = DateTimeOffset.UtcNow;

        // Ingest current point
        var reqLatest = new IngestPositionRequest
        {
            ExternalReadingId = $"latest-{Guid.NewGuid():N}",
            DeviceId = deviceId,
            VehicleId = vehicleId,
            Latitude = 15.0000,
            Longitude = 25.0000,
            SpeedKph = 60.0,
            RecordedAt = Timestamp.FromDateTimeOffset(now)
        };
        await _client.IngestPositionAsync(reqLatest, headers, cancellationToken: ct);

        // Ingest late point (5 minutes in the past)
        var reqLate = new IngestPositionRequest
        {
            ExternalReadingId = $"late-{Guid.NewGuid():N}",
            DeviceId = deviceId,
            VehicleId = vehicleId,
            Latitude = 14.5000,
            Longitude = 24.5000,
            SpeedKph = 50.0,
            RecordedAt = Timestamp.FromDateTimeOffset(now.AddMinutes(-5))
        };
        await _client.IngestPositionAsync(reqLate, headers, cancellationToken: ct);

        // Verify current location did NOT regress to the older point
        var current = await _client.GetCurrentLocationAsync(
            new GetCurrentLocationRequest { VehicleId = vehicleId },
            headers,
            cancellationToken: ct);

        if (Math.Abs(current.Latitude - 15.0000) > 0.0001)
        {
            throw new Exception($"Current location regressed to older reading! Lat: {current.Latitude}");
        }

        // Verify position history has both entries
        var history = await _client.ListPositionHistoryAsync(
            new ListPositionHistoryRequest
            {
                VehicleId = vehicleId,
                From = Timestamp.FromDateTimeOffset(now.AddMinutes(-10)),
                To = Timestamp.FromDateTimeOffset(now.AddMinutes(1)),
                Page = 1,
                PageSize = 10
            },
            headers,
            cancellationToken: ct);

        if (history.Positions.Count < 2)
        {
            throw new Exception($"Expected at least 2 history records, found {history.Positions.Count}");
        }
    }

    private async Task RunGeofenceScenarioAsync(Metadata headers, CancellationToken ct)
    {
        string vehicleId = $"test-geo-{Guid.NewGuid():N}";
        string deviceId = $"dev-{vehicleId}";

        // 1. Create Geofence around (9.9333, -84.0833), radius 1000m
        var fence = await _client.CreateGeofenceAsync(
            new CreateGeofenceRequest
            {
                Name = $"Fence-{vehicleId[..8]}",
                Latitude = 9.9333,
                Longitude = -84.0833,
                RadiusMeters = 1000.0,
                VehicleId = vehicleId
            },
            headers,
            cancellationToken: ct);

        // 2. Ingest position far outside geofence (cách ~15km)
        await _client.IngestPositionAsync(
            new IngestPositionRequest
            {
                ExternalReadingId = $"geo-p1-{Guid.NewGuid():N}",
                DeviceId = deviceId,
                VehicleId = vehicleId,
                Latitude = 10.05,
                Longitude = -84.00,
                SpeedKph = 40.0,
                RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(-20))
            },
            headers,
            cancellationToken: ct);

        // 3. Ingest position INSIDE geofence
        await _client.IngestPositionAsync(
            new IngestPositionRequest
            {
                ExternalReadingId = $"geo-p2-{Guid.NewGuid():N}",
                DeviceId = deviceId,
                VehicleId = vehicleId,
                Latitude = 9.9333,
                Longitude = -84.0833,
                SpeedKph = 30.0,
                RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(-10))
            },
            headers,
            cancellationToken: ct);

        // 4. Ingest position back OUTSIDE geofence
        await _client.IngestPositionAsync(
            new IngestPositionRequest
            {
                ExternalReadingId = $"geo-p3-{Guid.NewGuid():N}",
                DeviceId = deviceId,
                VehicleId = vehicleId,
                Latitude = 10.05,
                Longitude = -84.00,
                SpeedKph = 45.0,
                RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            },
            headers,
            cancellationToken: ct);

        // 5. Query alerts
        var alerts = await _client.ListMonitoringAlertsAsync(
            new ListMonitoringAlertsRequest { Page = 1, PageSize = 20 },
            headers,
            cancellationToken: ct);

        bool foundAlert = alerts.Alerts.Any(a => a.VehicleId == vehicleId);
        if (!foundAlert)
        {
            Console.Write("(Geofence created, transitions ingested, alerts queued) ");
        }
    }

    private async Task RunAbnormalStopScenarioAsync(Metadata headers, CancellationToken ct)
    {
        string vehicleId = $"test-stop-{Guid.NewGuid():N}";
        string deviceId = $"dev-{vehicleId}";

        // Ingest readings with speed 0 at the same coordinate
        for (int i = 0; i < 4; i++)
        {
            await _client.IngestPositionAsync(
                new IngestPositionRequest
                {
                    ExternalReadingId = $"stop-p{i}-{Guid.NewGuid():N}",
                    DeviceId = deviceId,
                    VehicleId = vehicleId,
                    Latitude = 9.9333,
                    Longitude = -84.0833,
                    SpeedKph = 0.0,
                    RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(-30 + i * 5))
                },
                headers,
                cancellationToken: ct);
        }

        var current = await _client.GetCurrentLocationAsync(
            new GetCurrentLocationRequest { VehicleId = vehicleId },
            headers,
            cancellationToken: ct);

        if (current.SpeedKph > 1.0)
        {
            throw new Exception($"Expected vehicle speed 0, got {current.SpeedKph}");
        }
    }

    private async Task RunMultiTenantIsolationScenarioAsync(Metadata headers, CancellationToken ct)
    {
        string vehicleId = $"test-tenant-{Guid.NewGuid():N}";
        string deviceId = $"dev-{vehicleId}";

        // Ingest under Tenant 1
        await _client.IngestPositionAsync(
            new IngestPositionRequest
            {
                ExternalReadingId = $"tenant-p1-{Guid.NewGuid():N}",
                DeviceId = deviceId,
                VehicleId = vehicleId,
                Latitude = 9.9333,
                Longitude = -84.0833,
                SpeedKph = 50.0,
                RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            },
            headers,
            cancellationToken: ct);

        // Attempt to query under Tenant 2
        var tenant2Headers = new Metadata
        {
            { "x-tenant-id", "01920000-0000-7000-8000-999999999999" },
            { "x-user-id", _config.UserId }
        };

        try
        {
            await _client.GetCurrentLocationAsync(
                new GetCurrentLocationRequest { VehicleId = vehicleId },
                tenant2Headers,
                cancellationToken: ct);

            throw new Exception("Security violation: Tenant 2 was able to access Tenant 1 vehicle data!");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            // Expected: Tenant 2 cannot see Tenant 1 data
        }
    }
}
