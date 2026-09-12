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
| `Storage__InputProvider` | Yes | No | `.env.local` | ConfigMap | `S3` for AKS R2 (`FileSystem` locally) |
| `Storage__S3__Bucket` | When `S3` | No | `.env.local` | ConfigMap | R2 bucket name |
| `Storage__S3__ServiceUrl` | When `S3` | No | `.env.local` | ConfigMap | `https://<account-id>.r2.cloudflarestorage.com` |
| `Storage__S3__Region` | When `S3` | No | `.env.local` | ConfigMap | `auto` for R2 |
| `Storage__S3__ForcePathStyle` | No | No | `.env.local` | ConfigMap | `true` |
| `Storage__S3__AccessKey` | When `S3` | Yes | Local `.env` | Key Vault | R2 API token access key |
| `Storage__S3__SecretKey` | When `S3` | Yes | Local `.env` | Key Vault | R2 API token secret key |

## Cloudflare R2 activation

Document OCR uses the AWS SDK's S3 protocol adapter; `S3` here means an
S3-compatible endpoint and does not require an AWS account. Cloudflare R2 uses
`Region=auto`, the account-specific R2 endpoint, and an R2 API token with
Object Read & Write permission for the document bucket.

Keep the default `FileSystem` provider for the current staging deployment until
the R2 bucket, endpoint and two R2 credential secrets are ready. The deployable
example is `../helm/r2-values.example.yaml`; copy it to a private values file,
replace the bucket/endpoint names, create the two Key Vault secrets, then deploy
it as a second Helm values file. `Storage__InputBridge__SigningKey` is only
needed for the filesystem upload bridge and is not required in S3/R2 mode.
Never commit R2 credentials or private values files.
