# IamTenant Deployment & Environment Configuration

Identity and Access Management service with exclusive ownership of AWS Cognito OIDC identity provider.

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Local `.env` | Key Vault | Neon Managed PostgreSQL |
| `AWS_REGION` | Yes | No | `.env.local` | ConfigMap | `ap-southeast-1` |
| `AWS_COGNITO_USER_POOL_ID` | Yes | Yes | Local `.env` | Key Vault | — |
| `AWS_COGNITO_CLIENT_ID` | Yes | Yes | Local `.env` | Key Vault | — |
| `AWS_COGNITO_CLIENT_SECRET` | Yes | Yes | Local `.env` | Key Vault | — |

## AWS IAM Least-Privilege Policy

The IAM User / Role assumed by `IamTenant` requires the permissions defined in [`deploy/aws/iam-tenant-cognito-policy.json`](../aws/iam-tenant-cognito-policy.json) to manage multi-tenant Cognito User Pools, App Clients, and execute Admin User / Auth workflows.
