# Regulatory Compliance RAG Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & RAG System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in .NET 10 `RegulatoryCompliance` implementation.

---

### Q1 (Junior): What is the role of `pgvector` in the Regulatory Compliance service?
**Answer**:  
`pgvector` is a PostgreSQL extension that enables storing vector embeddings (1536-dimensional vectors) directly within relational tables. Aurora uses `pgvector` with HNSW (Hierarchical Navigable Small World) cosine indexes to perform fast, multi-tenant vector similarity searches over thousands of customs regulations, tariff codes, and trade law articles.

---

### Q2 (Mid): How does the service ensure the LLM does not hallucinate fake legal articles?
**Answer**:  
Aurora enforces **Strict Citation Grounding**:
1. The RAG pipeline retrieves verbatim statute chunks from PostgreSQL and passes them into the prompt with strict instruction to cite only provided texts.
2. In the post-processing phase, the service compares all cited article numbers and excerpt strings against the actual retrieved `RegulatoryChunk` IDs.
3. If an evaluation finding lacks verified legal citations, it is flagged as ungrounded and routed to human review.

---

### Q3 (Mid): How does the compliance override mechanism work?
**Answer**:  
If a shipment is flagged (e.g. for dual-use technology or special permit requirements), an authorized compliance officer with capability `compliance:override` can submit an override request (`POST /api/v1/compliance/evaluations/{id}/override`). The request requires a mandatory legal justification and supporting document link, transitions the status to `Overridden`, logs the officer's `UserId`, and publishes an outbox event to unblock shipment booking.

---

### Q4 (Senior): Why store vectors in PostgreSQL (`pgvector`) instead of a standalone vector database like Pinecone or Milvus?
**Answer**:  
1. **ACID Transactions**: Vector chunks, documents, audit logs, and outbox events are committed in a single database transaction.
2. **Simplified Multi-Tenancy**: Standard relational queries seamlessly combine vector search with relational filters (e.g. `WHERE tenant_id = @tenantId AND document_status = 'ACTIVE'`).
3. **Operational Simplicity**: No external database to sync, backup, or monitor; uses the existing Neon PostgreSQL cluster with automated PITR.

---

### Q5 (System Design): What happens if the embeddings endpoint in `AiGovernance` is unavailable?
**Answer**:  
The retrieval engine uses a **Hybrid Fallback Strategy**:
If vector embedding generation fails, the system executes an exact SQL keyword and HS-code prefix search (`WHERE hs_code LIKE @codePrefix OR chunk_text ILIKE @keywords`). While less semantically nuanced than vector search, it guarantees that strict tariff and sanctions rules are still evaluated without crashing the compliance pipeline.
