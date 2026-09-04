# billing-service Deployment & Environment Configuration

Billing, invoicing, driver settlements, and tariff calculation service (deployed in `aks-core`).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `NODE_ENV` | Yes | No | `.env.local` | ConfigMap | `production` |
| `DATABASE_URL` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `GRPC_PORT` | Yes | No | `.env.local` | ConfigMap | `5004` |
| `REDIS_HOST` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net` |
| `REDIS_PORT` | Yes | No | `.env.local` | ConfigMap | `10000` |
| `REDIS_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
