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

The AI Governance deployment must set `IAM_TENANT_GRPC_ADDRESS` to the private
IamTenant endpoint exposed by the core cluster. In the demo environment that is
`static://10.10.0.63:5000`. The application default is for local development
only and points to `localhost:5001`. Kubernetes service DNS names cannot resolve
across the separate core and AI clusters; using a core-cluster service name from
the `aurora-ai` namespace causes tenant policy resolution to return
`POLICY_ERROR`. Both VNets must have connected peering for the private endpoint
to be reachable.

AI Governance also uses the AI cluster-local Redis service at
`redis.aurora.svc.cluster.local`. The tenant plan resolver falls back to IAM on
a cache failure, but production should keep both paths available.

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
5. If a completed OCR event exhausts its delivery retries, confirm the corpus
   version becomes `FAILED` with `CORPUS_PIPELINE_FAILED` instead of remaining
   `PENDING_OCR`.

The regression coverage is in
`src/dotnet/RegulatoryCompliance/Tests/DocumentOcrIntegrationConsumerTests.cs`
and `EmbeddingVectorTests.cs`, plus the platform policy test in the
AI Governance module.
