# negotiation-agent-service Deployment & Environment Configuration

Customer rate proposal and draft suggestion service (deployed in `aks-ai`).

## Business boundary

- `ACCEPT` is a recommendation only. The session becomes `PENDING_APPROVAL`; no shipment price is settled by this service.
- A staff member reviews the mail draft. The final shipment agreement is recorded separately in Billing with customer agreement evidence.
- New sessions remain unavailable in production until an authoritative shipment quote with approved list and floor prices is integrated. Existing sessions in production must carry `pricing_evidence_reference`, `pricing_approved_by`, and `pricing_approved_at` before processing an offer. Caller supplied prices are accepted only outside production.
- Each `SubmitOffer` requires a distinct `source_message_id`. Its original customer price and the suggested price are stored separately. Retries with the same source ID are rejected.
- Production gRPC calls require `x-tenant-id` and `x-internal-secret`. Staff BFF forwards the shared secret from `INTERNAL_SERVICE_SECRET`. Keep the Negotiation service reachable only by trusted internal callers.

## Database rollout

The initial migration `20260926090000_initial_negotiation` creates the schema for a new Negotiation database. The following migration `20260927090000_offer_provenance` adds offer provenance and unique indexes. Run `prisma migrate deploy` against a new database before deploying this service version.

For an existing Negotiation database, verify that its schema matches the initial migration, then mark the initial migration as already applied before running `prisma migrate deploy`. Inspect and resolve duplicate active sessions before the partial unique index is created. Do not run the initial migration directly on tables that already exist.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `NODE_ENV` | Yes | No | `.env.local` | ConfigMap | `production` |
| `PORT` | Yes | No | `.env.local` | ConfigMap | `8080` (HTTP) |
| `GRPC_PORT` | Yes | No | `.env.local` | ConfigMap | `5006` |
| `DATABASE_URL` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `INTERNAL_SERVICE_SECRET` | Yes in production | Yes | Local `.env` | Key Vault | Same trusted-service secret as Staff BFF |
| `REDIS_HOST` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net` |
| `REDIS_PORT` | Yes | No | `.env.local` | ConfigMap | `10000` |
| `REDIS_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `OPENAI_API_KEY` | Yes | Yes | Local `.env` | Key Vault | — |
