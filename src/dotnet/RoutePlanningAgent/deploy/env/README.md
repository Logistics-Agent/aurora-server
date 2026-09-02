# RoutePlanningAgent Deployment & Environment Configuration

Vehicle Routing Problem (VRP) solver and multi-stop logistics route optimizer (deployed in `aks-ai`).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `Redis__Host` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net:10000` |
| `Redis__Password` | Yes | Yes | Local `.env` | Key Vault | — |
| `Osrm__BaseUrl` | Yes | No | `.env.local` | ConfigMap | `http://osrm:5000` |
| `Vroom__BaseUrl` | Yes | No | `.env.local` | ConfigMap | `http://vroom:3000` |
