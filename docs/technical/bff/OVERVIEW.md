# Backend For Frontend (BFF) & API Gateway Suite — Overview

> **Service Layer**: Edge Routing, Micro-BFF Aggregation, Session Security & Protocol Translation  
> **Target Audience**: Frontend Engineers, Backend Architects, SREs, Security Auditors  
> **Source-of-Truth**: `src/dotnet/BFF/`, `API.Gateway`, `Staff.Bff`, `Admin.Bff`, `System.Bff`, `BuildingBlocks.BFF`.

---

## 1. Purpose & Architecture Overview

The Aurora Platform employs a **Micro-BFF (Backend-For-Frontend) Architectural Pattern** combined with a high-performance **YARP (Yet Another Reverse Proxy) API Gateway** written in .NET 8. 

Instead of routing all frontend clients (Staff Portal, Admin Portal, System Operations, Mobile App) to a monolithic API Gateway or exposing internal gRPC microservices directly, Aurora isolates concerns into dedicated persona-based Micro-BFFs:

```
                  ┌──────────────────────────────────────────────────┐
                  │                 Frontend Clients                 │
                  │   (Staff Web SPA, Admin Portal, Mobile Clients)  │
                  └─────────────────────────┬────────────────────────┘
                                            │ HTTPS (Cookie / Bearer)
                                            ▼
                  ┌──────────────────────────────────────────────────┐
                  │          API.Gateway (YARP Reverse Proxy)        │
                  │  - Path Routing (/api/v1/staff/*, /api/v1/admin) │
                  │  - Global Rate Limiting, SSL Termination, CORS   │
                  └─────┬───────────────────┼──────────────────┬─────┘
                        │                   │                  │
        ┌───────────────┘                   │                  └───────────────┐
        ▼                                   ▼                                  ▼
┌────────────────┐                 ┌────────────────┐                 ┌────────────────┐
│   Staff.Bff    │                 │   Admin.Bff    │                 │   System.Bff   │
│ (Freight Ops & │                 │ (User, Tenant  │                 │(Platform Audit │
│  Daily Tasks)  │                 │  & AI Config)  │                 │ & Telemetry)   │
└───────┬────────┘                 └────────┬───────┘                 └────────┬───────┘
        │                                   │                                  │
        └───────────────────────────────────┼──────────────────────────────────┘
                                            │ gRPC Internal Network
                                            ▼
              ┌───────────────────────────────────────────────────────────┐
              │           Internal Polyglot Microservice Mesh             │
              │  (IAM, Shipment, Mail, OCR, AI-Governance, Billing, etc.) │
              └───────────────────────────────────────────────────────────┘
```

---

## 2. Micro-BFF Persona Segregation

| Micro-BFF | Primary Target | Key Domain Responsibilities |
|---|---|---|
| **`Staff.Bff`** | Forwarding Agents, Logistics Operators, Dispatchers | Shipments booking, Document OCR correction, Mail triage & claims, Rate negotiation, GPS tracking, Live assistant chat. |
| **`Admin.Bff`** | Tenant Admins, HR / Operation Managers | User provisioning, Role & Capability delta updates, AI token budgets & model routing config, Mail server domain DNS setup. |
| **`System.Bff`** | Super Admins, SRE & Platform Engineers | Cross-tenant metrics, Platform-wide ingestion pipelines, System audit logs, Tenant lifecycle provisioning. |
| **`API.Gateway`** | Edge Entry Point | YARP-based high-throughput reverse proxy, SSL termination, global DDoS / Rate limiting, unified routing rules. |

---

## 3. Shared Foundation (`BuildingBlocks.BFF`)

All Micro-BFFs share core infrastructure provided by `BuildingBlocks.BFF`:
- **Security & Cookie Bridge**: Converts HttpOnly, Secure, SameSite cookies from browsers into internal Bearer tokens for downstream gRPC metadata headers.
- **gRPC Client Interceptors**: Injects Correlation ID, Tenant ID, and User ID into every outbound gRPC call.
- **Unified Error Normalization**: Intercepts `RpcException` and maps gRPC `StatusCode` to standard RFC 7807 `ProblemDetails` for clean frontend error handling.
- **Tenant Context Resolution**: Resolves tenant scope from subdomain, claims, or header (`X-Tenant-Id`).
- **Resilience & Rate Limiting**: ASP.NET Core 8 Rate Limiting middleware (Sliding window / Token bucket per tenant/IP).

---

## 4. Key Architectural Invariants

1. **No Business Logic in BFF**: The BFF acts purely as an API gateway, orchestrator, and protocol translator. Core domain state machines and database writes reside in their respective microservices.
2. **Strict Protocol Demarcation**: REST/JSON & WebSockets to the outside world; pure gRPC & Protobuf internally.
3. **Defense-in-Depth Authorization**: The BFF validates JWT claims and capability attributes (`[RequireCapability("...")]`) before making downstream gRPC invocations. Downstream microservices re-verify tenant scoping on every call.
