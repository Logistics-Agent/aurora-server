# Java notification-service Deployment & Environment Configuration

**Status: EXPERIMENTAL / INACTIVE (V1 AKS Skipped)**

Alternative Java Spring Boot implementation of Notification service. Contains deployment skeleton for future activation once gRPC `notification.proto` adapter and event contracts are proven.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `SPRING_PROFILES_ACTIVE` | Yes | No | `.env.local` | ConfigMap | `prod` |
| `SPRING_DATASOURCE_URL` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `SPRING_DATASOURCE_USERNAME` | Yes | Yes | Local `.env` | Key Vault | — |
| `SPRING_DATASOURCE_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `SPRING_RABBITMQ_HOST` | Yes | No | `.env.local` | ConfigMap | `rabbitmq.aurora.svc.cluster.local` |
| `SPRING_RABBITMQ_PORT` | Yes | No | `.env.local` | ConfigMap | `5672` |
| `SPRING_RABBITMQ_USERNAME` | Yes | No | `.env.local` | ConfigMap | `aurora_admin` |
| `SPRING_RABBITMQ_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `SPRING_REDIS_HOST` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net` |
| `SPRING_REDIS_PORT` | Yes | No | `.env.local` | ConfigMap | `10000` |
| `SPRING_REDIS_PASSWORD` | Yes | Yes | Local `.env` | Key Vault | — |
| `TELEGRAM_BOT_TOKEN` | Yes | Yes | Local `.env` | Key Vault | — |
