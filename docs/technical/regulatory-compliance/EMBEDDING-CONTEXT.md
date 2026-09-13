# Embedding execution context

The compliance RAG embedding provider receives an `EmbeddingInput` for every
text. Each input carries the tenant that owns the source chunk. This context
must be preserved when embedding is triggered by an OCR event or by the
background embedding worker; those paths do not have an HTTP current-user
context.

## Tenant-scoped embeddings

Tenant and knowledge chunks pass their owning `TenantId` to the provider. The
provider sends:

- `x-service-id: regulatory-compliance-rag`
- `x-tenant-id: <chunk tenant id>`

AI Governance evaluates this request against the tenant plan and quota. A
missing tenant on a tenant chunk is invalid and must not be replaced with a
different tenant from the process or host context.

The AI Governance deployment must set `IAM_TENANT_GRPC_ADDRESS` to
`static://iam-tenant.aurora.svc.cluster.local:5000`. The application default is
for local development only and points to `localhost:5001`; using that default
in the `aurora-ai` namespace causes tenant policy resolution to return
`POLICY_ERROR`.

## Platform embeddings

Platform chunks intentionally have no `TenantId`. They use the dedicated
internal workload identity:

- `x-service-id: regulatory-compliance-platform-rag`
- no `x-tenant-id`

AI Governance trusts this identity only for `compliance.embed`. It is separate
from the tenant workload identity so a missing tenant cannot silently bypass a
tenant's plan or quota.

## Operational verification

After deploying both compliance and AI Governance:

1. Publish or upload a tenant corpus document and wait for the OCR completion
   event.
2. Confirm the compliance version becomes `COMPLETED` and its chunks report
   completed embeddings.
3. Confirm the compliance log contains the OCR completion handling without
   `TENANT_NOT_FOUND`.
4. For platform corpus ingestion, confirm the platform worker identity is
   deployed with the AI Governance change before processing pending chunks.

The regression coverage is in
`src/dotnet/RegulatoryCompliance/Tests/DocumentOcrIntegrationConsumerTests.cs`
and `EmbeddingVectorTests.cs`, plus the platform policy test in the
AI Governance module.
