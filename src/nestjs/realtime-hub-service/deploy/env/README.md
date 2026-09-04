# realtime-hub-service Deployment & Environment Configuration

WebSocket hub broadcasting real-time telemetry, live map markers, and toast notifications (deployed in `aks-core`).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `NODE_ENV` | Yes | No | `.env.local` | ConfigMap | `production` |
| `PORT` | Yes | No | `.env.local` | ConfigMap | `8080` (WebSocket) |
| `GRPC_PORT` | Yes | No | `.env.local` | ConfigMap | `5008` |
| `REDIS_HOST` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net` |
| `REDIS_PORT` | Yes | No | `.env.local` | ConfigMap | `10000` |
| `REDIS_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
