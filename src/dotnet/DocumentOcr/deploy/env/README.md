# DocumentOcr Deployment & Environment Configuration

AI-driven Optical Character Recognition & bill-of-lading document processor (deployed in `aks-ai`).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `RabbitMQ__Host` | Yes | No | `.env.local` | ConfigMap | `10.10.2.50` (Core Internal LB) |
| `RabbitMQ__Port` | Yes | No | `.env.local` | ConfigMap | `5672` |
| `RabbitMQ__Username` | Yes | No | `.env.local` | ConfigMap | `aurora_admin` |
| `RabbitMQ__Password` | Yes | Yes | Local `.env` | Key Vault | — |
| `AzureStorage__AccountName` | Yes | No | `.env.local` | ConfigMap | `stauroradatademo` |
| `AzureStorage__ContainerName` | Yes | No | `.env.local` | ConfigMap | `ocr-docs` |
