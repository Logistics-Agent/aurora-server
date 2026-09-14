# AI Governance Redis and IAM runbook

## Runtime topology

- AI workloads resolve Redis through `redis.aurora.svc.cluster.local:6379` in the AI AKS cluster.
- Redis authentication uses the `redis-password` Azure Key Vault secret. External Secrets materializes it as `redis-secret/password` for Redis and as service-specific environment variables for clients.
- AI Governance reaches IamTenant through the private internal load balancer at `static://10.10.0.63:5000`. The address is reachable only through the peered Azure virtual networks.

## Deployment order

1. Confirm `redis-password` exists in Key Vault and all ExternalSecret resources are Ready. Do not print the secret value.
2. Apply `infra/k8s/redis-auth.yaml`; this creates the Redis ExternalSecret and updates only the Redis Deployment and Service.
3. Wait for `redis-secret` to exist and for the AUTH-enabled Redis rollout to become Ready.
4. Roll out AI Governance, Document OCR, Regulatory Compliance, and Customer Assistant so every client uses `redis.aurora.svc.cluster.local` with the same credential.
5. Submit a new corpus document and verify the pipeline reaches OCR, chunking, embedding, and `COMPLETED`.

Do not enable Redis AUTH before the client credentials are present. A partially deployed change causes authentication failures until all clients are restarted with the matching secret.

## Verification

Expected evidence:

- Redis rejects unauthenticated `PING` and accepts authenticated `PING`.
- AI Governance logs `Governance ALLOWED` for `compliance.embed` and no longer logs `POLICY_ERROR` or `LazyInitializationException`.
- Regulatory Compliance logs successful corpus ingestion.
- Corpus list/detail APIs report non-zero chunk and embedded chunk counts with status `COMPLETED`.

## Rollback

Roll back clients and Redis together. Restoring an unauthenticated Redis while clients still send a password causes `AUTH called without any password configured`; removing client credentials while Redis still requires AUTH causes `NOAUTH Authentication required`.
