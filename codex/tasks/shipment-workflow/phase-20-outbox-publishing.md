# Phase 20 - Outbox Publishing and Notification Integration

## Status

Completed

## Objective

Publish Shipment integration events already persisted in `outbox_messages` through MassTransit and RabbitMQ without coupling Shipment Workflow to a consumer database.

## Scope

* Add an explicit registry for all Shipment integration event contract types.
* Process pending outbox rows in bounded, ordered batches.
* Prevent concurrent workers from selecting the same rows.
* Publish the deserialized contract through MassTransit.
* Mark successful rows as processed.
* Persist bounded retry count and diagnostic error for failed rows.
* Register and configure the background worker in Shipment Workflow.
* Verify real Shipment-to-Notification broker delivery with local infrastructure.

## Completion Criteria

* All 11 Shipment event contracts are publishable.
* Unknown or malformed messages are not marked successful.
* Broker failures leave messages pending and increment retry state.
* Concurrent workers use PostgreSQL row locking with skip-locked semantics.
* Existing Shipment and Notification tests remain passing.
* Shipment and Notification build successfully.
* A real ShipmentCreated outbox row reaches Notification through RabbitMQ.
* No service reads another service database.
* No schema migration is created unless the existing outbox schema proves insufficient.

## Evidence

### Work Completed

* Added an allowlist registry for all 11 Shipment integration event contracts.
* Added a scoped outbox processor with bounded ordered batches and PostgreSQL `FOR UPDATE SKIP LOCKED`.
* Added MassTransit publication, successful processing timestamps, bounded retry counts, and persisted error diagnostics.
* Added and registered the configurable Shipment outbox hosted worker.
* Verified a real ShipmentCreated event from the Shipment database through RabbitMQ into the Notification consumer, inbox, projection, and InApp delivery.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Infrastructure/BackgroundJobs/ShipmentIntegrationEventTypeRegistry.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/BackgroundJobs/ShipmentIntegrationEventPublisher.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/BackgroundJobs/ShipmentOutboxProcessor.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/BackgroundJobs/ShipmentOutboxPublisherBackgroundService.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/BackgroundJobs/ShipmentOutboxPublisherOptions.cs`
* `src/dotnet/ShipmentWorkflow/Program.cs`
* `src/dotnet/ShipmentWorkflow/appsettings.json`
* `src/dotnet/ShipmentWorkflow/Tests/ShipmentOutboxPublisherTests.cs`
* Shipment requirements, specification, gap analysis, plan, and this task file.

### Commands Executed

```bash
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --filter ShipmentOutboxPublisherTests
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
dotnet build src/dotnet/Contracts/Shipment.Contracts/Shipment.Contracts.csproj
dotnet build src/dotnet/Notification/Notification.csproj
dotnet test src/dotnet/Notification/Tests/Notification.Tests.csproj
dotnet ef migrations list --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --startup-project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --no-build
docker compose -f docker-compose.dev.yml ps
docker exec -i aurora-shipment-postgres psql -U postgres -d aurora_shipment_workflow
docker exec -i aurora-notification-postgres psql -U postgres -d aurora_notification
dotnet run --project src/dotnet/Notification/Notification.csproj --no-build --no-launch-profile
dotnet run --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --no-build --no-launch-profile
git diff --check
```

The first non-interactive smoke seed commands failed because the command wrapper stripped nested SQL quotes. The same SQL was rerun successfully through interactive psql stdin; this was a command invocation issue, not an application failure.

Final parallel validation exposed recursive test-output copying because the Web SDK included colocated `Tests/bin` JSON files as production content. Commit `e574f54` excludes all `Tests/**/*` content/resource items from both production projects; the same parallel build/test commands then passed.

### Build Result

Passed. Shipment Workflow built 3 projects with 0 errors and 0 warnings. Notification built 3 projects with 0 errors and 0 warnings. Shipment.Contracts built with 0 errors and 0 warnings.

### Test Result

Passed. Focused outbox tests passed 16 cases. Full Shipment Workflow regression passed 99 tests. Notification regression passed 29 tests.

### Runtime Result

Passed with local PostgreSQL, Redis, and RabbitMQ healthy. The Shipment worker published smoke event `20000000-0000-0000-0000-000000000004`; its outbox row was processed with retry count 0. Notification persisted the matching consumed-event record and created a `ShipmentCreated` InApp notification with status `Sent`. Both services shut down normally after validation, and all smoke rows were removed.

### Migration Result

No migration was required. The applied initial schema already contains `ProcessedAt`, `RetryCount`, and `Error`. Migration list remains `20260713201248_InitialShipmentWorkflow` and `20260714042938_ExpandShipmentWorkflowMvp`.

### Remaining Issues

No Phase 20 blocker. Future services consuming Shipment events must add their own consumers and idempotency storage during their assigned phases.
