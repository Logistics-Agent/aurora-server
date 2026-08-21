# Phase 08 — Program Configuration and Migration

## Status

Not Started

## Goal

Configure runtime.

## Prerequisites

Phase 07.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/regulatory-compliance-rag.md`

## Existing State

Production implementation has not started for this service.

## Scope

Wire the complete service process, configure provider/local defaults and health checks, add a
dedicated vector-capable PostgreSQL service, generate/apply the initial migration, and smoke-start
gRPC, ingestion/evaluation workers, messaging, and outbox publication.

## Required Behavior

* Register shared auth/exception interceptors, current-user context, gRPC, DbContext, ingestion,
  chunking, embedding/vector, retrieval, evaluation, TimeProvider, and outbox services.
* Register MassTransit only for approved compliance events/consumers; no direct OCR/Shipment calls
  except explicit gRPC/event contracts and no cross-service database connection strings.
* Bind and validate source/chunk limits, vector dimension/model, retrieval topK/threshold,
  provider timeouts, worker batches, retries, leases, and outbox options on startup.
* Default automated/local operation to deterministic fake embedding/evaluation providers; real
  provider keys come only from environment/secret providers and are never committed.
* Add a dedicated `aurora-regulatory-compliance` PostgreSQL container/database on a non-conflicting
  port using an image/extension compatible with the selected vector implementation.
* Generate exactly one `InitialRegulatoryCompliance` migration. Review source/version/chunk/vector,
  evaluation/finding/citation/trace, inbox/outbox tables, visibility filters, indexes, and extension.
* Confirm the target database, list/apply migrations, inspect tables/indexes/vector extension, and
  never reset another service database.
* Start locally with PostgreSQL, Redis, RabbitMQ, and fake providers; prove health, worker queries,
  vector round-trip, gRPC host, and clean shutdown.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet ef migrations add InitialRegulatoryCompliance --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --output-dir Infrastructure/Persistences/Migrations
dotnet ef migrations list --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet ef database update --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Migration is applied only to the confirmed compliance database and runtime passes with fakes.
* Create local commit `feat(compliance): configure startup and migration`.

## Work Log

### Completed

Not started.

### Files Changed

None.

### Commands Executed

None.

### Build Result

Not started.

### Test Result

Not started.

### Runtime Result

Not started.

### Migration Result

Not started.

### Remaining Issues

Phase has not started.
