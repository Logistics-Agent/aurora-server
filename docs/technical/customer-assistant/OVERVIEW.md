# AI Customer & Operational Assistant — Service Overview

> **Service Layer**: Conversational AI, Live Tool Execution & Intent Orchestration  
> **Target Audience**: Technical Recruiters, AI Engineers, Frontend Integrators  
> **Source-of-Truth**: `src/nestjs/customer-assistant-service`, `ToolRegistryService`, `IntentClassifier`, `ShipmentLookupTool`, `BillingSummaryTool`.

---

## 1. Service Purpose & Problem Solved

In global freight operations, customers and logistics staff constantly query multiple fragmented systems to track containers, check invoice balances, and verify customs restrictions. Traditional rule-based chatbots fail on complex queries, while naive LLM wrappers hallucinate shipment locations and leak cross-tenant information.

The **Customer Assistant Service** solves this through **Tool-Augmented Retrieval & Grounded Conversational AI**:
- **Multi-Turn Context & Intent Routing**: Classifies queries into domain intents (Shipment Tracking, Invoices/Billing, Regulatory/Customs, FAQ).
- **Sandboxed Tool Execution**: The LLM does not guess answers; it invokes verified backend tools (`ShipmentLookupTool`, `BillingSummaryTool`, `RegulatorySearchTool`).
- **Strict Tenant & Customer Bounding**: Tool parameters are cryptographically anchored to the authenticated user's session context.

---

## 2. Architecture & Tech Stack

```
[ Customer Portal / Staff SPA ]
              │
              ▼ (WebSocket / REST Chat Endpoint)
[ Customer Assistant Service (NestJS Port 50053) ]
  ├── 1. Intent Classifier & Policy Evaluator
  ├── 2. Conversational Orchestrator (Multi-turn History)
  ├── 3. Tool Registry & Execution Sandbox
  │      ├── ShipmentLookupTool ──(gRPC)──> ShipmentWorkflow
  │      ├── BillingSummaryTool  ──(REST)──> BillingService
  │      └── RegulatorySearchTool ──(gRPC)──> RegulatoryCompliance
  └── 4. Central AiGovernance Linkage (capability: "assistant.chat")
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | Node.js 20, NestJS 10, TypeScript |
| **Communication** | REST API, WebSocket Gateway, internal gRPC clients |
| **Tool Calling Pattern** | Function Calling / Tool Definition Interfaces (`ITool`) |
| **AI Integration** | Central `AiGovernance` gateway |
| **State Storage** | Redis (Session memory / multi-turn buffer) + PostgreSQL (Audit logs) |

---

## 3. Owned Data & Schema Boundaries

- **`ChatSessions`**: Multi-turn conversation identifiers, user session IDs, tenant IDs.
- **`ChatMessages`**: User queries, assistant responses, tool execution payloads, and token consumption statistics.
- **`ToolInvocations`**: Audit records of every tool executed by the assistant (arguments, execution duration, and sanitized output).

---

## 4. Tool Registry & Integration Surface

The `ToolRegistryService` exposes strictly verified capabilities:
- **`ShipmentLookupTool`**: Resolves shipment status, container number, vessel ETA, and delivery milestones.
- **`BillingSummaryTool`**: Resolves outstanding invoice balances and payment due dates.
- **`RegulatorySearchTool`**: Retrieves customs tariff regulations and import/export compliance requirements.
- **`KnowledgeSearchTool`**: Semantic search across platform knowledge bases.

---

## 5. Security & Invariants

1. **Zero Direct Database Writes**: The assistant has read-only access to operational systems; it cannot autonomously cancel shipments or alter invoices.
2. **Contextual Tenant Scoping**: Tools inject `tenantId` from authenticated context; user prompt cannot override the tenant filter.
3. **Current Maturity**: Production-ready core orchestrator with live tool calling integration across Shipment and Billing services.
