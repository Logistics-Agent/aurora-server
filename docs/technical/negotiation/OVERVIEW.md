# Freight Rate Negotiation Agent — Service Overview

> **Service Layer**: Dynamic Pricing, Bidding & Human-in-the-Loop Negotiation  
> **Target Audience**: Technical Recruiters, Sales Engineers, AI Architects  
> **Source-of-Truth**: `src/nestjs/negotiation-agent-service`, `NegotiationStrategyDomainService`, `protos/negotiation.proto`.

---

## 1. Service Purpose & Problem Solved

Freight forwarding rate negotiations are traditionally slow, manual, and prone to margin erosion. Sales representatives exchange dozens of emails back and forth, frequently quoting sub-optimal rates or violating company floor margins.

The **Negotiation Agent Service** solves this through a **Deterministic Financial Engine + AI Natural Language Hybrid Architecture**:
- **Deterministic Pricing Guardrails**: AI models are **strictly forbidden** from generating or altering price numbers. All counter-offers, bottom prices, and margin thresholds are calculated deterministically in TypeScript.
- **Dynamic Concession Curves**: Evaluates customer tiers, negotiation round counts ($N \le 3$), initial offers, and floor margins to generate optimal counter-proposals.
- **Human-in-the-Loop (HitL) Draft Linkage**: Translates negotiation outcomes into natural language draft suggestions (`SuggestedReplyDto`) that operational staff review, edit, and send via `MailService`.

---

## 2. Architecture & Tech Stack

```
[ Inbound Quotation Email / Customer Request ]
                      │
                      ▼
[ Negotiation Agent Service (NestJS Port 50052) ]
  ├── 1. Deterministic Strategy Engine (Accept, Counter, Handoff)
  ├── 2. Concession Curve Calculator (Margin Floor Guard)
  ├── 3. LLM Natural Language Generator (via AiGovernance)
  └── 4. Persisted Session Repository (Prisma + PostgreSQL)
                      │
                      ▼
[ Staff.Bff / NegotiationsController ]
  ├── Staff clicks [Create Mail Draft from Negotiation]
  └── Ingests into active EmailThread as EmailDraft (SourceType="NEGOTIATION")
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | Node.js 20, NestJS 10, TypeScript |
| **Communication Protocol** | gRPC (`protos/negotiation.proto`), REST health endpoints |
| **ORM & Database** | Prisma ORM, PostgreSQL (Neon Serverless SSL) |
| **AI Integration** | Central `AiGovernance` gRPC service (`capability: "negotiation.speech"`) |
| **BFF Client** | `Staff.Bff.Controllers.NegotiationsController` (.NET 10) |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`NegotiationSessions`**: Tracks `ShipmentId`, `CustomerId`, `CustomerTier`, `ListPrice`, `BottomPrice`, `CurrentRound`, `Status` (`OPEN`, `ACCEPTED`, `HANDOFF`, `REJECTED`).
- **`NegotiationMessages`**: History of rounds, incoming offer prices, counter-offer prices, decisions, AI speech text, and suggested reply DTOs.

---

## 4. API & Contract Surface

Exposed via `protos/negotiation.proto` (`NegotiationService`):
- `SubmitOffer`: Evaluates incoming customer offer against bottom price and returns decision (`ACCEPT`, `COUNTER_OFFER`, `HUMAN_HANDOFF`).
- `GetDraftSuggestion`: Retrieves persisted, validated draft reply for human-in-the-loop mail creation.
- `GetSessionHistory`: Returns full round-by-round negotiation audit trail.

---

## 5. Security & Invariants

1. **Zero Financial Hallucination**: Financial numbers come directly from `NegotiationStrategyDomainService`, never from LLM token completions.
2. **Floor Margin Invariant**: Counter-offers will **never** go below `BottomPrice`.
3. **Current Maturity**: Production-ready deterministic strategy with complete BFF integration and human-in-the-loop mail draft linkage.
