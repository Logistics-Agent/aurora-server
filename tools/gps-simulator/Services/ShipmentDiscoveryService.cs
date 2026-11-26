using System.Text.Json;
using GpsSimulator.Models;
using Grpc.Core;
using ShipmentWorkflow.Grpc;

namespace GpsSimulator.Services;

public class ShipmentDiscoveryService
{
    private readonly ShipmentWorkflowService.ShipmentWorkflowServiceClient? _client;
    private readonly Metadata _headers;

    public ShipmentDiscoveryService(
        ShipmentWorkflowService.ShipmentWorkflowServiceClient? client,
        Metadata headers)
    {
        _client = client;
        _headers = headers;
    }

    public async Task<List<ShipmentCandidate>> DiscoverAsync(
        SimulationConfig config,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ShipmentCandidate>();

        if (config.Mode == SimulationMode.Single)
        {
            var single = await FetchSingleShipmentAsync(config.SingleShipmentId!, config.AutoAssignVehicle, cancellationToken);
            candidates.Add(single);
            return candidates;
        }

        if (!string.IsNullOrWhiteSpace(config.ShipmentsFilePath))
        {
            return await LoadFromFileAsync(config.ShipmentsFilePath, config, cancellationToken);
        }

        // Auto or Fleet mode: Query via gRPC ListShipments
        if (_client == null)
        {
            throw new InvalidOperationException("ShipmentWorkflowService client is required for auto/fleet discovery.");
        }

        int page = 1;
        const int limit = 50;
        int maxVehicles = config.VehiclesLimit ?? int.MaxValue;

        while (candidates.Count < maxVehicles && !cancellationToken.IsCancellationRequested)
        {
            var request = new ListShipmentsRequest
            {
                Page = page,
                Limit = limit
            };

            ListShipmentsResponse response;
            try
            {
                response = await _client.ListShipmentsAsync(
                    request,
                    _headers,
                    deadline: DateTime.UtcNow.AddSeconds(10),
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] ListShipments failed on page {page}: {ex.Message}");
                break;
            }

            if (response.Shipments == null || response.Shipments.Count == 0)
            {
                break;
            }

            foreach (var s in response.Shipments)
            {
                // Flow 1.3:
                // 1. Check vehicleId
                string? vehicleId = string.IsNullOrWhiteSpace(s.VehicleId) ? null : s.VehicleId;
                if (vehicleId == null)
                {
                    if (config.AutoAssignVehicle)
                    {
                        vehicleId = $"veh-auto-{s.Id[..Math.Min(8, s.Id.Length)]}";
                    }
                    else
                    {
                        // Skip shipment without vehicle assignment by default
                        continue;
                    }
                }

                // 2. Check RouteId or Locations
                bool hasRoute = !string.IsNullOrWhiteSpace(s.RouteId);
                bool hasLocations = s.Locations != null && s.Locations.Count >= 2;

                if (!hasRoute && !hasLocations && !config.AutoAssignVehicle)
                {
                    // Skip if neither route nor locations found
                    continue;
                }

                var candidate = new ShipmentCandidate
                {
                    ShipmentId = s.Id,
                    VehicleId = vehicleId,
                    RouteId = s.RouteId,
                    CustomerName = s.CustomerName,
                    Status = s.Status,
                    Locations = s.Locations?.Select(l => new WaypointLocation(l.Latitude, l.Longitude, l.Sequence, l.Name)).ToList()
                               ?? new List<WaypointLocation>()
                };

                candidates.Add(candidate);

                if (candidates.Count >= maxVehicles)
                {
                    break;
                }
            }

            if (response.Page >= response.TotalPages || response.Shipments.Count < limit)
            {
                break;
            }

            page++;
        }

        // If in Fleet mode with --vehicles N requested, and discovered candidates are fewer than requested:
        if (config.Mode == SimulationMode.Fleet && config.VehiclesLimit.HasValue && candidates.Count > 0 && candidates.Count < config.VehiclesLimit.Value)
        {
            int needed = config.VehiclesLimit.Value;
            Console.WriteLine($"Discovered {candidates.Count} eligible shipment(s). Scaling up to {needed} virtual vehicles using discovered trajectories...");
            var baseList = new List<ShipmentCandidate>(candidates);
            int idx = 0;
            while (candidates.Count < needed)
            {
                var template = baseList[idx % baseList.Count];
                idx++;
                int cloneNum = candidates.Count + 1;
                candidates.Add(new ShipmentCandidate
                {
                    ShipmentId = template.ShipmentId,
                    VehicleId = $"sim-veh-{cloneNum:D4}",
                    RouteId = template.RouteId,
                    CustomerName = $"{template.CustomerName} (Virtual #{cloneNum})",
                    Status = template.Status,
                    Locations = new List<WaypointLocation>(template.Locations)
                });
            }
        }
        else if (candidates.Count == 0 && config.Mode == SimulationMode.Fleet && config.VehiclesLimit.HasValue)
        {
            // If database has 0 shipments, but user requested e.g. --mode fleet --vehicles 100 for load testing
            int needed = config.VehiclesLimit.Value;
            Console.WriteLine($"No shipments found in Shipment Workflow. Generating {needed} synthetic vehicles along default corridor for load testing...");
            for (int i = 1; i <= needed; i++)
            {
                candidates.Add(new ShipmentCandidate
                {
                    ShipmentId = $"00000000-0000-0000-0000-{i:D12}",
                    VehicleId = $"sim-veh-{i:D4}",
                    RouteId = null,
                    CustomerName = $"Virtual Vehicle #{i}",
                    Status = "IN_TRANSIT",
                    Locations = new List<WaypointLocation>()
                });
            }
        }

        return candidates;
    }

    private async Task<ShipmentCandidate> FetchSingleShipmentAsync(
        string shipmentId,
        bool autoAssign,
        CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return new ShipmentCandidate
            {
                ShipmentId = shipmentId,
                VehicleId = shipmentId, // Backward compatibility fallback
                CustomerName = "Fallback Offline Shipment"
            };
        }

        try
        {
            var s = await _client.GetShipmentAsync(
                new GetShipmentRequest { Id = shipmentId },
                _headers,
                deadline: DateTime.UtcNow.AddSeconds(10),
                cancellationToken: cancellationToken);

            string vehicleId = s.VehicleId;
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                // In single mode backward compatibility: if shipment has no vehicleId, fallback to shipmentId
                vehicleId = shipmentId;
                Console.WriteLine($"[INFO] Shipment {shipmentId} has no assigned VehicleId. Using shipmentId as vehicleId (backward compatible).");
            }

            return new ShipmentCandidate
            {
                ShipmentId = s.Id,
                VehicleId = vehicleId,
                RouteId = s.RouteId,
                CustomerName = s.CustomerName,
                Status = s.Status,
                Locations = s.Locations?.Select(l => new WaypointLocation(l.Latitude, l.Longitude, l.Sequence, l.Name)).ToList()
                           ?? new List<WaypointLocation>()
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Shipment '{shipmentId}' not found in Shipment Workflow service.");
        }
    }

    private async Task<List<ShipmentCandidate>> LoadFromFileAsync(
        string filePath,
        SimulationConfig config,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Shipments file not found: {filePath}");
        }

        string json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var candidates = new List<ShipmentCandidate>();

        // Try parsing string array ["uuid1", "uuid2"]
        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (ids != null && ids.Count > 0)
            {
                foreach (var id in ids)
                {
                    if (config.VehiclesLimit.HasValue && candidates.Count >= config.VehiclesLimit.Value) break;
                    if (_client != null)
                    {
                        try
                        {
                            var cand = await FetchSingleShipmentAsync(id, config.AutoAssignVehicle, cancellationToken);
                            candidates.Add(cand);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARN] Could not fetch shipment {id}: {ex.Message}");
                        }
                    }

                    candidates.Add(new ShipmentCandidate
                    {
                        ShipmentId = id,
                        VehicleId = $"sim-veh-{id[..Math.Min(8, id.Length)]}",
                        CustomerName = "File Loaded Shipment"
                    });
                }
                return candidates;
            }
        }
        catch
        {
            // Try parsing object array [{"shipmentId": "...", "vehicleId": "..."}]
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<ShipmentCandidateDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (config.VehiclesLimit.HasValue && candidates.Count >= config.VehiclesLimit.Value) break;
                    candidates.Add(new ShipmentCandidate
                    {
                        ShipmentId = item.ShipmentId ?? item.Id ?? Guid.NewGuid().ToString(),
                        VehicleId = item.VehicleId ?? item.ShipmentId ?? item.Id ?? Guid.NewGuid().ToString(),
                        RouteId = item.RouteId,
                        CustomerName = item.CustomerName ?? "File Loaded"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            throw new FormatException($"Failed to parse shipments JSON file '{filePath}': {ex.Message}");
        }

        return candidates;
    }

    private record ShipmentCandidateDto(string? Id, string? ShipmentId, string? VehicleId, string? RouteId, string? CustomerName);
}

