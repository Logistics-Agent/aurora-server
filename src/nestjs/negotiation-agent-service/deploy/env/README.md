# negotiation-agent-service Deployment & Environment Configuration

AI rate negotiation, carrier bidding, and spot contract settlement service (deployed in `aks-ai`).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `NODE_ENV` | Yes | No | `.env.local` | ConfigMap | `production` |
| `PORT` | Yes | No | `.env.local` | ConfigMap | `8080` (HTTP) |
| `GRPC_PORT` | Yes | No | `.env.local` | ConfigMap | `5006` |
| `DATABASE_URL` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `REDIS_HOST` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net` |
| `REDIS_PORT` | Yes | No | `.env.local` | ConfigMap | `10000` |
| `REDIS_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `OPENAI_API_KEY` | Yes | Yes | Local `.env` | Key Vault | — |
