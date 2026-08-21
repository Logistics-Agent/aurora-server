# Phase 10 - Owned Service Event Consumers

## Status

Completed

## Goal

Expand Notification event consumption across the five owned services without coupling to their databases or runtime implementations.

## Scope

* Complete Shipment lifecycle and document notification mappings.
* Consume GPS monitoring alerts, excluding high-volume position updates.
* Consume Document OCR completion and failure events.
* Consume Regulatory Compliance evaluation completion and failure events.
* Preserve tenant-scoped preferences, inbox idempotency, and provider-neutral delivery.
* Add focused tests and real RabbitMQ delivery evidence.

## Constraints

* Do not read or write another service database.
* Use existing versioned integration contracts.
* Preserve existing `NotificationEventType` numeric values.
* Do not include raw OCR extracted JSON or unnecessary sensitive data in notification bodies.
* Do not notify on every GPS position update.
* Keep notification title and body within persisted domain limits.
* Do not require paid provider credentials.

## Completion Criteria

* All approved event contracts have registered MassTransit consumers.
* Event mappings preserve event ID, contract version, tenant, optional shipment reference, and occurrence time.
* Duplicate events remain idempotent.
* Existing and new tests pass.
* Runtime starts and RabbitMQ delivery is verified when local infrastructure is available.
* No migration is generated unless the persisted schema actually changes.

## Work Log

### Baseline

* Build passed with 0 errors and 0 warnings.
* Notification tests passed: 29.
* Existing unrelated deletion `docs/owned-services-postman.md` is excluded from this phase.

### Work Completed

* Added five missing Shipment lifecycle/document mappings and consumer bindings.
* Added GPS monitoring-alert consumption while intentionally excluding position updates.
* Added Document OCR completion/failure consumption without copying extracted JSON.
* Added Regulatory Compliance completion/failure consumption.
* Generalized the event envelope/projector for notifications without a Shipment reference.
* Preserved existing enum numeric values and added new values from 9 through 13.
* Kept tenant-scoped preference lookup and inbox idempotency atomic in Notification persistence.
* Verified real RabbitMQ delivery for GPS, OCR, and Compliance events.

### Files Changed

* src/dotnet/Notification/Notification.csproj
* src/dotnet/Notification/Program.cs
* src/dotnet/Notification/Application/Consumers/ShipmentNotificationConsumer.cs
* src/dotnet/Notification/Application/Consumers/GpsNotificationConsumer.cs
* src/dotnet/Notification/Application/Consumers/DocumentOcrNotificationConsumer.cs
* src/dotnet/Notification/Application/Consumers/ComplianceNotificationConsumer.cs
* src/dotnet/Notification/Application/Services/ShipmentEventNotificationFactory.cs
* src/dotnet/Notification/Application/Services/GpsEventNotificationFactory.cs
* src/dotnet/Notification/Application/Services/DocumentOcrEventNotificationFactory.cs
* src/dotnet/Notification/Application/Services/ComplianceEventNotificationFactory.cs
* src/dotnet/Notification/Application/Services/IntegrationEventNotificationEnvelope.cs
* src/dotnet/Notification/Application/Services/IntegrationEventNotificationProjector.cs
* src/dotnet/Notification/Domain/Enums/NotificationEventType.cs
* src/dotnet/Notification/Tests/Application/ShipmentEventNotificationFactoryTests.cs
* src/dotnet/Notification/Tests/Application/OwnedServiceEventNotificationFactoryTests.cs
* src/dotnet/Notification/Tests/Application/IntegrationEventNotificationProjectorTests.cs
* src/dotnet/Notification/Tests/Integration/NotificationPostgresIntegrationTests.cs
* src/dotnet/Notification/Tests/Integration/OwnedServiceRabbitMqIntegrationTests.cs
* src/dotnet/Notification/Tests/Notification.Tests.csproj
* codex/specs/notification.md
* codex/plan.md
* codex/tasks/notification/phase-10-owned-service-event-consumers.md

### Commands Executed

```bash
git status --short
git branch --show-current
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj --no-build
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj --filter FullyQualifiedName~OwnedServiceRabbitMqIntegrationTests
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --no-restore
dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj --no-restore
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj --no-restore
dotnet ef migrations list --project src/dotnet/Notification/Notification.csproj --startup-project src/dotnet/Notification/Notification.csproj
docker compose -f docker-compose.dev.yml ps
timeout 15s env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:6101 dotnet run --project src/dotnet/Notification/Notification.csproj --no-build --no-launch-profile
git diff --check
```

### Build Result

Passed: 6 projects, 0 errors, 0 warnings.

### Test Result

Passed: Notification 41, Shipment 99, GPS 50, Document OCR 63, Regulatory Compliance 57. The focused real RabbitMQ consumer test also passed.

### Runtime Result

Passed on temporary port 6101. All four MassTransit consumer endpoints were configured, RabbitMQ connected, PostgreSQL queries succeeded, and shutdown was caused only by the intentional timeout.

### Migration Result

No migration generated. Event enum values are persisted as strings in existing 80-character columns, and `ShipmentId` was already nullable. Existing migration `20260719124939_InitialNotification` remains applied.

### Remaining Issues

* The process already running on port 6001 predates this build and must be restarted to load the new consumers.
* Current local preferences cover only `ShipmentCreated`; users must opt in to the new event types through `UpsertNotificationPreference`.
* Real SMTP still requires deployment-provided configuration and credentials.

### Commit Hash

`3d2fa07` - `feat(notification): consume owned service events`
