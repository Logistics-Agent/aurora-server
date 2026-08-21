# Phase 08 — Program and Migration

## Status

Not Started

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
