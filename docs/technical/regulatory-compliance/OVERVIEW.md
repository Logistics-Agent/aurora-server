# Regulatory Compliance RAG Service — Service Overview

> **Service Layer**: Cross-Border Compliance, Sanctions Screening & Legal RAG  
> **Target Audience**: Technical Recruiters, Legal-Tech Engineers, System Architects  
> **Source-of-Truth**: `src/dotnet/RegulatoryCompliance`, `ComplianceEvaluation`, `ComplianceCitation`, `KnowledgeChunk`, `pgvector`, `protos/regulatory_compliance.proto`.

---

## 1. Service Purpose & Problem Solved

Cross-border freight forwarding is heavily regulated by international trade laws, customs tariffs (HS Codes), sanctions lists (OFAC, EU), and hazardous materials regulations. Non-compliance results in severe financial penalties, vessel detentions, and cargo confiscation. Traditional rule engines cannot comprehend nuanced legal text, while pure LLMs hallucinate non-existent customs articles.

The **Regulatory Compliance Service** combines **Deterministic Trade Rules + Vector RAG Knowledge Retrieval + Legal Citation Tracing**:
- **Grounded Legal Knowledge Base**: Ingests national customs tariffs, dual-use export control laws, and free-trade agreements into vector chunks using `pgvector`.
- **Mandatory Legal Citation Tracing**: Every compliance finding or restriction returned by the system is backed by exact, traceable legal citations (`SourceDocument`, `ArticleNumber`, `ExactExcerpt`).
- **Human Compliance Override (`compliance:override`)**: Compliance officers can review flagged shipments and apply structured overrides with mandatory legal justifications and audit logs.

---

## 2. Architecture & Tech Stack

```
[ ShipmentWorkflow / Customs Filing Flow ]
                   │
                   ▼ (gRPC Port 5007)
┌─────────────────────────────────────────────────────────────┐
│             Regulatory Compliance Microservice (.NET 10)    │
│  ├── Document Ingestion & Chunking Pipeline                 │
│  ├── pgvector Semantic Retrieval Engine                     │
│  ├── Deterministic Sanctions & Dual-Use Filter              │
│  ├── Citation Tracker & Grounding Verifier                  │
│  ├── Compliance Override & Audit Engine                     │
│  └── Transactional Outbox (RabbitMQ Publisher)              │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]        [ Central AiGovernance ]
    (pgvector chunks, Evaluations)     (gRPC capability: "compliance.rag")
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Vector Database & Storage**| PostgreSQL 16 with `pgvector` extension (HNSW index, cosine distance) |
| **ORM & Schema** | Entity Framework Core 10, Npgsql |
| **AI Integration** | Central `AiGovernance` gateway for embeddings & compliance synthesis |
| **Event Broker** | RabbitMQ, Transactional Outbox Pattern |

---

## 3. Owned Data & Schema Boundaries

The service strictly owns:
- **`RegulatoryDocuments` & `Versions`**: Customs codes, bilateral trade agreements, tariff schedules, import restriction catalogs.
- **`RegulatoryChunks`**: Vector embeddings (1536-dim vectors), chunk text, article metadata, and vector similarity indexes.
- **`ComplianceEvaluations`**: Tracks `ShipmentId`, `OriginCountry`, `DestinationCountry`, `HsCode`, `Status` (`Compliant`, `Flagged`, `RequiresReview`, `Overridden`), and composite risk score.
- **`ComplianceFindings` & `Citations`**: Granular violation flags, severity, suggested remediations, and source legal citations.
- **`RetrievalTraces`**: Audit logs of vector similarity scores, query text, and retrieved chunks.

---

## 4. API & Contract Surface

Exposed via `protos/regulatory_compliance.proto` (`RegulatoryComplianceService`):
- `EvaluateCompliance`: Evaluates a proposed shipment against export control rules, sanctions, and customs tariffs.
- `GetEvaluation`: Queries evaluation status, flags, and legal citations.
- `OverrideCompliance`: Allows an authorized compliance officer (`compliance:override`) to override a restriction with mandatory legal justification.
- `SearchRegulatoryKnowledge`: Semantic search across active trade regulations.

---

## 5. Security & Invariants

1. **Zero Citation-Free Claims**: AI responses lacking traceable source legal citations are discarded fail-closed.
2. **Override Capability Protection**: Non-compliance flags can only be overridden by users with explicit capability `compliance:override`.
3. **Current Maturity**: Production-ready RAG evaluation and citation engine with vector indexing and outbox publishing.
