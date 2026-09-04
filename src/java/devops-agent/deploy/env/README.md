# devops-agent Deployment & Environment Configuration

Intelligent DevOps, CI/CD telemetry analysis, and automated operations agent (deployed in `aks-ai`).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `SPRING_PROFILES_ACTIVE` | Yes | No | `.env.local` | ConfigMap | `prod` |
| `SPRING_DATASOURCE_URL` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `SPRING_DATASOURCE_USERNAME` | Yes | Yes | Local `.env` | Key Vault | — |
| `SPRING_DATASOURCE_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `SPRING_REDIS_HOST` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net` |
| `SPRING_REDIS_PORT` | Yes | No | `.env.local` | ConfigMap | `10000` |
| `SPRING_REDIS_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `GITHUB_TOKEN` | Yes | Yes | Local `.env` | Key Vault | — |
