# Regulatory Compliance RAG Service Specification

## Implementation Status

Completed on `feat/regulatory-compliance-rag`. The service exposes tenant-safe gRPC ingestion,
retrieval, evaluation, and lookup APIs; owns a dedicated PostgreSQL schema; uses deterministic local
embedding/evaluation providers behind replaceable interfaces; and publishes completion/failure
events through a transactional MassTransit outbox. The final suite passes 51 Compliance tests,
including PostgreSQL `real[]`, concurrent idempotency, full cited evaluation, and RabbitMQ delivery.

## Purpose

Regulatory Compliance RAG Service exists to retrieve regulations and evaluate compliance with evidence and citations.

## Boundaries

This is an independent service with its own database and deployment boundary. It communicates through gRPC APIs and integration events. It must not read or write another service database.

## Owned Data

Owns regulatory documents, chunks, embeddings, retrieval traces, compliance results.

## Data Not Owned

Does not own OCR execution, shipment aggregate, notification delivery.

## Dependencies

Depends on shared authentication/tenant context, service-specific PostgreSQL storage, RabbitMQ/MassTransit for events, and explicit contracts from producing services.

## Contracts

Contracts must contain cross-service messages only. They must not include EF entities, DbContexts, repositories, handlers, workers, or runtime configuration.

## APIs

Expose service-owned APIs only. APIs must accept external IDs for cross-service references and must enforce tenant context.

## Event Consumers

Consumers must be idempotent, retry-aware, and safe for duplicate delivery.

## Event Publishers

Publish service-owned events through transactional outbox when persistence and publication must be reliable.

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

## Security

Do not commit credentials. Validate untrusted input. Do not expose stack traces. Protect tenant isolation.

## Validation

Validate required fields, enum values, external reference IDs, provider payloads, and state changes.

## Runtime Configuration

Runtime configuration includes database connection, RabbitMQ, Redis when needed, provider settings, logging, and health checks.

## Migration Requirements

Create migrations only for this service database. Confirm target database before applying updates.

## Test Requirements

Use unit tests for domain rules and integration tests for persistence, events, idempotency, and tenant isolation. Automated tests must not require paid external credentials; use deterministic fakes.

## Definition of Done

The service builds, starts, migrates its database, passes tests, enforces tenant isolation, handles retries/idempotency, and communicates only through approved contracts/events.

## Assumptions

Provider integrations are abstracted and currently use deterministic local implementations. Cloud
providers can replace them through deployment configuration without exposing credentials in source.

## Explicitly Excluded Responsibilities

Responsibilities owned by other services remain excluded even when this service stores external IDs.
