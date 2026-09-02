# MailService Deployment & Environment Configuration

Enterprise Mail Management backend providing security screening pipelines, Stalwart MTA synchronization, and tenant mailbox authorization.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `Redis__Host` | Yes | No | `.env.local` | ConfigMap | `redis-aurora-shared-demo.southeastasia.redis.azure.net:10000` |
| `Redis__Password` | Yes | Yes | Local `.env` | Key Vault | — |
| `Stalwart__AdminUrl` | Yes | No | `.env.local` | ConfigMap | `http://stalwart:8080` |
| `Stalwart__AdminApiKey` | Yes | Yes | Local `.env` | Key Vault | — |
| `ClamAV__Host` | Yes | No | `.env.local` | ConfigMap | `clamav` |
| `SpamAssassin__Host` | Yes | No | `.env.local` | ConfigMap | `spamassassin` |
| `IamTenant__GrpcUrl` | Yes | No | `.env.local` | ConfigMap | `http://iam-tenant:5000` |
