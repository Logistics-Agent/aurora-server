# .NET Notification Deployment & Environment Configuration

Primary and active V1 Notification service implementation with gRPC (`notification.proto` port 5000), MassTransit event consumers, SMTP dispatch, and In-App notification tracking.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `RabbitMQ__Host` | Yes | No | `.env.local` | ConfigMap | `rabbitmq.aurora.svc.cluster.local` |
| `RabbitMQ__Port` | Yes | No | `.env.local` | ConfigMap | `5672` |
| `RabbitMQ__Username` | Yes | No | `.env.local` | ConfigMap | `aurora_admin` |
| `RabbitMQ__Password` | Yes | Yes | Local `.env` | Key Vault | — |
| `Smtp__Host` | Yes | No | `.env.local` | ConfigMap | `stalwart-ingress.aurora.svc.cluster.local` |
| `Smtp__Port` | Yes | No | `.env.local` | ConfigMap | `25` |
| `Smtp__Password` | Yes | Yes | Local `.env` | Key Vault | — |
