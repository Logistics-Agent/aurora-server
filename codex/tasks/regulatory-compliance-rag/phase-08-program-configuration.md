# Phase 08 — Program Configuration and Migration

## Status

Completed

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

Phases 01-07 provide the service host, contracts, model, persistence, ingestion, vector retrieval,
and deterministic compliance evaluation. Phase 08 wires those components into the runnable process.

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

* Wired gRPC, shared authentication/error interceptors, current-user services, PostgreSQL,
  MassTransit, deterministic embedding, ingestion, retrieval, evaluation, health checks, and
  TimeProvider into the service process.
* Added bounded embedding and transactional outbox background workers. The outbox uses an
  allowlisted event registry, PostgreSQL `FOR UPDATE SKIP LOCKED`, bounded retries, and MassTransit.
* Added validated runtime configuration for model dimensions, batches, polling, provider timeout,
  retrieval thresholds, and outbox retry behavior without committing provider secrets.
* Added a dedicated PostgreSQL 16 Docker service/database on host port 5437. PostgreSQL `real[]`
  is the selected bounded local vector representation, so no pgvector extension is required.
* Generated and reviewed the single initial migration, confirmed the target database, applied it,
  inspected tables/indexes/vector storage, and proved a transactional vector round trip.
* Smoke-started the host with PostgreSQL, Redis, and RabbitMQ. Health was `Healthy`; gRPC HTTP/2,
  embedding/outbox worker queries, RabbitMQ bus startup, and graceful bus shutdown were observed.

### Files Changed

* `docker-compose.dev.yml`
* `src/dotnet/RegulatoryCompliance/Program.cs`
* `src/dotnet/RegulatoryCompliance/appsettings.json`
* `src/dotnet/RegulatoryCompliance/appsettings.Development.json`
* `src/dotnet/RegulatoryCompliance/Infrastructure/BackgroundJobs/RegulatoryComplianceRuntimeOptions.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/BackgroundJobs/ComplianceEmbeddingBackgroundService.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/BackgroundJobs/ComplianceOutboxPublisher.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/BackgroundJobs/RegulatoryComplianceDbHealthCheck.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/Persistences/Migrations/20260723135052_InitialRegulatoryCompliance.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/Persistences/Migrations/20260723135052_InitialRegulatoryCompliance.Designer.cs`
* `src/dotnet/RegulatoryCompliance/Infrastructure/Persistences/Migrations/RegulatoryComplianceDbContextModelSnapshot.cs`
* `codex/tasks/regulatory-compliance-rag/phase-08-program-configuration.md`
* `codex/tasks/README.md`
* `codex/plan.md`

### Commands Executed

```bash
docker compose -f docker-compose.dev.yml config
docker compose -f docker-compose.dev.yml up -d regulatory-compliance-postgres
docker compose -f docker-compose.dev.yml ps regulatory-compliance-postgres
docker exec aurora-regulatory-compliance-postgres psql -U postgres -d aurora_regulatory_compliance -c "SELECT current_database(), current_user;"
dotnet ef migrations add InitialRegulatoryCompliance --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --output-dir Infrastructure/Persistences/Migrations -- --environment Development
dotnet ef migrations list --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj -- --environment Development
dotnet ef database update --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --startup-project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj -- --environment Development
docker exec aurora-regulatory-compliance-postgres psql ...
dotnet build src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj
dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj --no-restore
dotnet run --project src/dotnet/RegulatoryCompliance/RegulatoryCompliance.csproj --no-build --launch-profile http
curl --http2-prior-knowledge --fail http://localhost:5093/health
curl --http2-prior-knowledge --fail http://localhost:5093/
```

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 40 tests, 0 failed, 0 warnings.

### Runtime Result

Passed. The service listened on `http://localhost:5093`; HTTP/2 `/health` returned `Healthy` and
the root returned `Regulatory Compliance RAG gRPC Service`. Logs proved the MassTransit bus started,
both workers queried their PostgreSQL queues, and `SIGTERM` produced graceful application and bus
shutdown. RabbitMQ logs confirmed authentication as application `RegulatoryCompliance`.

### Migration Result

Applied `20260723135052_InitialRegulatoryCompliance` to the confirmed local
`aurora_regulatory_compliance` database. Migration list shows it applied. PostgreSQL inspection
found 9 service tables plus `__EFMigrationsHistory`, 38 indexes/constraints, and embedding storage
as nullable `real[]`; an insert/select inside a rolled-back transaction returned `{0.25,0.5,0.75}`.

### Remaining Issues

No Phase 08 blocker. Production embedding/evaluation providers remain intentionally unconfigured;
the deterministic local providers are the approved default until cloud credentials and adapters
are supplied through deployment configuration.
