# GPS Tracking and Monitoring Service Specification

## Purpose

GPS Tracking and Monitoring answers "where is the assigned vehicle now?" It ingests
trusted device readings, stores immutable position history, maintains a current-location
snapshot, evaluates operational monitoring rules, and publishes consumer-safe events.

## Implementation Status

Completed on `feat/gps-tracking`. All ten planned phases are implemented. The service has a
dedicated PostgreSQL schema from `20260721042104_InitialGpsTracking`, a gRPC host on local
port 5091, Shipment event consumers, monitoring workers, and transactional GPS event outbox.
Validation passes 50 GPS tests, including PostgreSQL migration/tenant/idempotency/cascade
coverage and real RabbitMQ publication proof. Shipment and Notification regressions also pass.

## Boundaries

This is an independent service with its own database and deployment boundary. It communicates through gRPC APIs and integration events. It must not read or write another service database.

## Owned Data

Owns GPS readings, current location snapshots, vehicle-shipment assignment references, geofences, monitoring alerts.

The MVP model contains:

* `GpsPosition`: immutable, idempotent device reading.
* `CurrentLocation`: latest accepted reading per tenant and vehicle.
* `VehicleShipmentAssignment`: local projection of Shipment `RouteAssigned`, cancellation,
  and completion events. Shipment and route identifiers are external references only.
* `Geofence` and `GeofencePresence`: circular monitoring boundary and per-vehicle state.
* `MonitoringAlert`: deduplicated signal-loss, abnormal-stop, geofence-entry, and
  geofence-exit alert.
* `ConsumedIntegrationEvent`: inbox receipt for idempotent Shipment event consumption.
* `OutboxMessage`: atomic GPS event publication record.

## Data Not Owned

Does not own route planning, ETA ownership, shipment lifecycle, cost estimation.

## Dependencies

Depends on shared authentication/tenant context, service-specific PostgreSQL storage, RabbitMQ/MassTransit for events, and explicit contracts from producing services.

## Contracts

Contracts must contain cross-service messages only. They must not include EF entities, DbContexts, repositories, handlers, workers, or runtime configuration.

## APIs

Expose service-owned APIs only. APIs must accept external IDs for cross-service references and must enforce tenant context.

The MVP gRPC surface is:

* `IngestPosition`: stores one idempotent reading and returns the accepted position.
* `GetCurrentLocation`: resolves one vehicle or shipment within the current tenant.
* `ListPositionHistory`: bounded, paged, deterministic position history.
* `CreateGeofence`, `ListGeofences`, and `SetGeofenceActive`: tenant-safe geofence management.
* `ListMonitoringAlerts` and `ResolveMonitoringAlert`: tenant-safe alert operations.

Client requests never contain `TenantId`. Position ingestion does not accept a client
controlled `ShipmentId`; the service derives it from an active assignment.

History requests require exactly one of vehicle ID or shipment ID, accept a maximum
seven-day range, and cap page size at 500.

## Event Consumers

Consumers must be idempotent, retry-aware, and safe for duplicate delivery.

GPS consumes `RouteAssignedEvent`, `ShipmentCancelledEvent`, and
`ShipmentCompletedEvent`. Consumers validate trusted event tenant/aggregate IDs, project
only local assignment references, and never query Shipment Workflow storage.

## Event Publishers

Publish service-owned events through transactional outbox when persistence and publication must be reliable.

GPS publishes versioned `GpsPositionUpdatedEvent` and `GpsMonitoringAlertRaisedEvent`
contracts. The outbox and business records commit atomically. A bounded background
publisher uses explicit type allowlisting and PostgreSQL row locking; it does not call
Realtime Hub directly.

## Domain Model

Domain models must express service-owned responsibilities only and keep providers behind interfaces.

## Persistence

Use a dedicated database. No cross-service foreign keys. Store external references as IDs.

## Tenant Behavior

All tenant-owned data is scoped by tenant from the authenticated current-user context or trusted event metadata. Client-provided tenant IDs are not trusted.

## Idempotency

Commands and event consumers that can be retried must use request IDs, event IDs, or deterministic natural keys where applicable.

## Retry Behavior

Transient provider and broker failures must be retried with bounded attempts and recorded errors.

Device retries are deduplicated by `(TenantId, DeviceId, ExternalReadingId)`. Shipment
events are deduplicated by `(SourceEventType, SourceEventId)`. Outbox publication records
retry count and the latest bounded error message.

## Security

Do not commit credentials. Validate untrusted input. Do not expose stack traces. Protect tenant isolation.

## Validation

Validate required fields, enum values, external reference IDs, provider payloads, and state changes.

Coordinates use latitude `[-90, 90]` and longitude `[-180, 180]`. Speed and accuracy
cannot be negative. Heading is `[0, 360)`. Readings more than five minutes in the future
or older than thirty days are rejected. Current location advances only when a reading is
newer than the stored snapshot; accepted late readings remain in history.

Circular geofences use a positive radius capped at 100 kilometres. Monitoring thresholds
are configuration-driven. Repeated active alerts are deduplicated until resolved or the
underlying state changes.

## Runtime Configuration

Runtime configuration includes database connection, RabbitMQ, Redis when needed, provider settings, logging, and health checks.

## Migration Requirements

Create migrations only for this service database. Confirm target database before applying updates.

## Test Requirements

Use unit tests for domain rules and integration tests for persistence, events, idempotency, and tenant isolation. Automated tests must not require paid external credentials; use deterministic fakes.

## Definition of Done

The service builds, starts, migrates its database, passes tests, enforces tenant isolation, handles retries/idempotency, and communicates only through approved contracts/events.

The local definition of done also requires an applied migration against the confirmed
`aurora_gps_tracking` development database, PostgreSQL-backed isolation and idempotency
tests, RabbitMQ outbox publication proof when infrastructure is available, and no
regression in owned Shipment/Notification builds.

## Assumptions

Provider integrations are abstracted and can be replaced with fakes in tests.

## Explicitly Excluded Responsibilities

Responsibilities owned by other services remain excluded even when this service stores external IDs.
