# Phase 08 — Program and Migration

## Status

Completed

## Goal

Configure startup and migration.

## Prerequisites

Phase 07.

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/logistics-architecture.md`
* `codex/specs/document-ocr.md`

## Existing State

Production implementation has not started for this service.

## Scope

Wire the complete service process, add dedicated local PostgreSQL infrastructure, generate and
review one initial OCR migration, apply it only to the confirmed OCR database, and smoke-start
gRPC, workers, and messaging.

## Required Behavior

* Register shared auth/exception interceptors, current-user context, gRPC service, DbContext,
  application services, provider/content interfaces, TimeProvider, workers, and outbox publisher.
* Register MassTransit/RabbitMQ only for approved OCR events/consumers; do not call Shipment or
  Compliance databases and do not create their consumers here.
* Bind and validate document limits, confidence threshold, provider selection, retry/lease,
  worker batch, and outbox options on startup.
* Use environment/provider configuration for production secrets; commit local placeholders only.
* Add `aurora-document-ocr` PostgreSQL to development Compose on a non-conflicting port with a
  dedicated `aurora_document_ocr` database and volume.
* Generate exactly one `InitialDocumentOcr` migration, review all tables/indexes/FKs/JSON fields,
  list migrations before and after, and apply only after confirming current database/container.
* Start the service with local PostgreSQL, Redis, and RabbitMQ; prove workers query successfully
  with the deterministic local provider and record the actual runtime result.

## Constraints

* Independent service and database.
* No direct Shipment Workflow database access.
* Preserve tenant isolation.
* Use contracts/events for cross-service communication.
* Keep providers behind interfaces and fakes available for tests.

## Validation Commands

```bash
dotnet ef migrations add InitialDocumentOcr --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj --output-dir Infrastructure/Persistences/Migrations
dotnet ef migrations list --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet ef database update --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
```

## Completion Criteria

* Scope is complete.
* Build succeeds.
* Relevant tests pass or absence is recorded for early foundation phases.
* Task file and plan are updated with real command evidence.
* Migration is applied to the confirmed OCR database and runtime starts without paid credentials.
* Create local commit `feat(ocr): configure startup and migration`.

## Work Log

### Completed

Configured the complete Document OCR process with shared gRPC authentication/error interceptors,
tenant current-user context, PostgreSQL persistence, MassTransit/RabbitMQ, deterministic local
provider/content reader, job/retry worker, and outbox publisher. Bound and validated document,
provider, lease/retry, and outbox settings. Added dedicated local PostgreSQL infrastructure,
generated and reviewed one initial migration, applied it to the confirmed OCR database, and
smoke-started the service without paid-provider credentials.

### Files Changed

* `docker-compose.dev.yml`
* `src/dotnet/DocumentOcr/Application/Providers/DocumentProcessingOptions.cs`
* `src/dotnet/DocumentOcr/Program.cs`
* `src/dotnet/DocumentOcr/appsettings.json`
* `src/dotnet/DocumentOcr/appsettings.Development.json`
* `src/dotnet/DocumentOcr/Infrastructure/Persistences/Migrations/20260722035404_InitialDocumentOcr.cs`
* `src/dotnet/DocumentOcr/Infrastructure/Persistences/Migrations/20260722035404_InitialDocumentOcr.Designer.cs`
* `src/dotnet/DocumentOcr/Infrastructure/Persistences/Migrations/DocumentOcrDbContextModelSnapshot.cs`
* `codex/plan.md`

### Commands Executed

```bash
git status --short
dotnet build src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj
docker compose -f docker-compose.dev.yml config --quiet
docker compose -f docker-compose.dev.yml up -d document-ocr-postgres redis rabbitmq
docker compose -f docker-compose.dev.yml ps
docker exec aurora-document-ocr-postgres psql -U postgres -d aurora_document_ocr -c "SELECT current_database(), current_user;"
dotnet ef migrations list --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet ef migrations add InitialDocumentOcr --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj --output-dir Infrastructure/Persistences/Migrations
dotnet ef database update --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet ef migrations has-pending-model-changes --project src/dotnet/DocumentOcr/DocumentOcr.csproj --startup-project src/dotnet/DocumentOcr/DocumentOcr.csproj
dotnet run --no-build --project src/dotnet/DocumentOcr/DocumentOcr.csproj --launch-profile http
curl --http2-prior-knowledge --max-time 5 http://localhost:5092/
docker exec aurora-rabbitmq rabbitmqctl list_connections name peer_host peer_port state
```

### Build Result

Passed: 3 projects built with 0 errors and 0 warnings.

### Test Result

Passed: 55 tests with 0 failed and 0 warnings.

### Runtime Result

Passed. The process remained running with the gRPC/HTTP2 endpoint on port 5092, returned
`Document OCR gRPC Service`, maintained a running RabbitMQ connection, and stopped cleanly on
SIGINT. PostgreSQL, Redis, and RabbitMQ were healthy; background polling produced no runtime error.

### Migration Result

Created and applied `20260722035404_InitialDocumentOcr` only to confirmed database
`aurora_document_ocr` on the dedicated `aurora-document-ocr-postgres` container. Migration list
shows it applied, PostgreSQL contains the expected four service tables plus migration history,
and EF reports no pending model changes.

### Remaining Issues

None.
