# Real-Time GPS Tracking & Geofencing Service — Service Overview

> **Service Layer**: IoT Telematics, Spatial Geofencing & Live Tracking  
> **Target Audience**: Technical Recruiters, IoT/Spatial Engineers, System Architects  
> **Source-of-Truth**: `src/dotnet/GpsTracking`, `GpsPosition.cs`, `Geofence.cs`, `CurrentLocation.cs`, `MonitoringAlert.cs`, `protos/gps_tracking.proto`.

---

## 1. Service Purpose & Problem Solved

Real-time visibility into vehicle locations, container ETAs, and cargo security is essential for modern freight forwarders and shippers. Fleet telematics generate high-frequency location streams that easily overload relational transactional databases if not architecturally isolated.

The **GPS Tracking Service** provides **High-Throughput Telematics Ingestion + Spatial Geofencing + Event-Driven Alerts**:
- **High-Frequency Ingestion**: Ingests vehicle and IoT sensor pings (lat, lng, speed, heading, altitude) via gRPC and message brokers.
- **In-Memory Hot Path**: Maintains latest positions in Redis (`CurrentLocation`) for sub-millisecond map queries.
- **Geofencing & Breach Detection**: Evaluates circular and polygon geofences (hubs, ports, customer facilities) to detect entry, exit, and dwell times.
- **Safety & Anomaly Alerts**: Detects over-speeding, extended idling, and route deviations, triggering real-time WebSocket alerts via RealtimeHub.

---

## 2. Architecture & Tech Stack

```
[ IoT Telematics / Driver Mobile App ]
                  │
                  ▼ (gRPC Port 5004 / MQTT / REST)
┌─────────────────────────────────────────────────────────────┐
│                 GpsTracking Microservice (.NET 10)          │
│  ├── High-Throughput Ping Ingestion Pipeline                │
│  ├── Spatial Geofence Evaluator (Circular & Polygon)        │
│  ├── Anomaly & Over-speeding Alert Detector                 │
│  ├── Hot-Path Redis Cache Updater                           │
│  └── Transactional Outbox (RabbitMQ Publisher)              │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]          [ Redis 7 Cache ]
     (Positions, Geofences, Alerts)    (Hot CurrentLocation)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Spatial Calculations**| Haversine distance, Ray-Casting Point-in-Polygon (PIP) algorithm |
| **Persistence & Caching**| PostgreSQL 16 (Neon SSL), Redis 7 (Latest vehicle coordinates cache) |
| **Messaging & Events** | RabbitMQ (`GeofenceEnteredEvent`, `GeofenceExitedEvent`, `SpeedAlertEvent`) |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`GpsPositions`**: Historical location log (`VehicleId`, `Latitude`, `Longitude`, `SpeedKmh`, `Heading`, `RecordedAt`).
- **`CurrentLocations`**: Hot-state table and Redis key tracking latest vehicle coordinate, battery level, and active shipment.
- **`Geofences`**: Circular (center lat/lng + radius meters) and Polygon (WKT coordinates) definitions.
- **`GeofencePresences`**: Tracks active inside/outside state and dwell durations per vehicle.
- **`MonitoringAlerts`**: Triggered safety alerts (`OverSpeed`, `GeofenceBreach`, `UnauthorizedStop`).

---

## 4. API & Contract Surface

Exposed via `protos/gps_tracking.proto` (`GpsTrackingService`):
- `IngestGpsPosition`: Ingests single or batch vehicle telematics pings.
- `GetLatestPosition`: Retrieves current location and heading (sub-millisecond from Redis cache).
- `GetPositionHistory`: Returns time-windowed breadcrumb trail for route replay.
- `CreateGeofence`: Defines circular or polygon boundary.
- `ListAlerts`: Queries active fleet safety alerts.

---

## 5. Security & Invariants

1. **Hot/Cold Storage Separation**: High-frequency raw pings write to partitioned historical tables; live queries hit Redis `CurrentLocation`.
2. **Multi-Tenant Boundary**: All geofences and vehicle positions are strictly bounded by `TenantId`.
3. **Current Maturity**: Production-ready telematics ingestion, geofencing, and real-time alert generation.
