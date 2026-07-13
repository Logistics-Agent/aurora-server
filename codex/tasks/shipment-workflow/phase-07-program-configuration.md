# Phase 07 — Program Configuration

## Status

Completed

## Goal

Configure dependency injection, middleware, infrastructure, and gRPC endpoints for Shipment Workflow.

## Prerequisites

* Phase 05 — Persistence
* Phase 06 — Proto Contract

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`
* Existing `Program.cs` files in the repository
* Existing IAM and tenant service startup conventions

## Scope

Primary file:

```text
src/dotnet/ShipmentWorkflow/Program.cs
```

Configuration files:

```text
src/dotnet/ShipmentWorkflow/appsettings.json
src/dotnet/ShipmentWorkflow/appsettings.Development.json
```

## Required Registrations

Register the repository-equivalent implementations for:

```text
gRPC
AuthInterceptor
ExceptionInterceptor
Shared services
Current-user service
MediatR
ShipmentWorkflowDbContext
PostgreSQL
MassTransit
RabbitMQ
Outbox background worker
Health checks, when required by repository conventions
```

## Required Endpoint

```csharp
app.MapGrpcService<ShipmentGrpcService>();
```

## Configuration Requirements

The service must have configuration for:

```text
PostgreSQL connection string
RabbitMQ
Redis, when required by shared services
Authentication
Logging
```

Do not commit secrets.

## Constraints

* Follow existing repository startup conventions.
* Do not introduce a second dependency-registration pattern.
* Do not hard-code credentials.
* Do not create migrations.
* Do not implement shipment business operations in `Program.cs`.
* Do not modify unrelated services.

## Validation

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

Run the service when the local dependencies are available:

```bash
dotnet run \
  --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

## Completion Criteria

* Required dependencies are registered.
* DbContext can be resolved.
* MediatR handlers can be resolved.
* gRPC endpoint is mapped.
* Interceptors are registered.
* Service builds.
* Service starts when required infrastructure is available.

## Work Log

### Completed

* Configured ShipmentWorkflow `Program.cs` following IAM startup conventions.
* Registered gRPC with `AuthInterceptor` and `ExceptionInterceptor`.
* Registered shared services, MassTransit/RabbitMQ, MediatR, ShipmentWorkflowDbContext, PostgreSQL, and shipment number generator.
* Registered `ExceptionInterceptor` explicitly so gRPC can resolve it.
* Mapped `ShipmentGrpcService`.
* Removed the template Greeter proto/service references from the ShipmentWorkflow project.
* Confirmed no ShipmentWorkflow outbox background worker exists yet, so none was registered.
* Confirmed Docker infrastructure containers were healthy before runtime validation.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Program.cs`
* `src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `src/dotnet/ShipmentWorkflow/appsettings.Development.json`
* `src/dotnet/ShipmentWorkflow/Protos/greet.proto`
* `src/dotnet/ShipmentWorkflow/Services/GreeterService.cs`
* `codex/tasks/phase-07-program-configuration.md`
* `codex/plan.md`

### Commands Executed

* `git status --short`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `docker compose -f docker-compose.dev.yml ps`
* `dotnet run --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`

The runtime command stayed running until manually stopped with Ctrl-C.

### Build Result

Passed. `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 0 errors and 0 warnings.

### Runtime Result

Passed for startup smoke validation. Docker infrastructure was healthy and `dotnet run --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` stayed running until manually stopped.

### Remaining Issues

No Phase 07 startup blocker remains. ShipmentWorkflow outbox background worker is not registered because it has not been implemented yet.

