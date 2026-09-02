# Admin.Bff Deployment & Environment Configuration

Backend-For-Frontend dedicated to Admin dashboard workflows, tenant management, and system auditing.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `Redis__Host` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net:10000` |
| `Redis__Password` | Yes | Yes | Local `.env` | Key Vault | — |
| `Jwt__SecretKey` | Yes | Yes | Local `.env` | Key Vault | — |
| `GrpcServices__*` | Yes | No | `.env.local` | ConfigMap | Downstream Kubernetes CoreDNS / localhost stubs |
