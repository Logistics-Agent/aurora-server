# Intelligent Route Planning & Risk Governance — Service Overview

> **Service Layer**: Vehicle Routing, Optimization Algorithms & Risk-Based Governance  
> **Target Audience**: Technical Recruiters, Operations Research Engineers, System Architects  
> **Source-of-Truth**: `src/dotnet/RoutePlanningAgent`, `Route.cs`, `RiskAssessment.cs`, `TenantRiskPolicy.cs`, `TenantRiskPolicyConfig.cs`, `VroomClient.cs`, `OsrmClient.cs`, `protos/route_planning.proto`.

---

## 1. Service Purpose & Problem Solved

Traditional logistics dispatching relies on either rigid manual dispatching or naive shortest-path heuristics that ignore vehicle capacities, delivery time windows, traffic anomalies, and toll costs. Furthermore, many enterprise systems enforce mandatory manager approvals on every dispatch change, creating severe bottlenecks that cause missed delivery deadlines.

The **RoutePlanningAgent Service** provides **VROOM/OSRM Multi-Vehicle Route Optimization + Risk-Based Operational Governance**:
- **Mathematical Optimization Engine**: Solves Capacitated Vehicle Routing Problems with Time Windows (CVRPTW) via VROOM and OSRM distance matrix solvers.
- **Risk-Based Operational Governance**: Replaces mandatory manager approvals with a 4-tier risk governance engine (`LOW` $\rightarrow$ Auto, `MEDIUM` $\rightarrow$ Staff Acknowledge, `HIGH` $\rightarrow$ Manager Approval, `CRITICAL` $\rightarrow$ Hard Block).
- **Tenant Policy Versioning**: Enables enterprise tenants to define custom threshold rules and weights, while strictly maintaining immutable policy versioning.
- **Centralized AI Integration**: Calls the central `AiGovernance` gateway for traffic risk predictions (`capability: "route.plan"`), completely decoupling AI models from the route planning database.

---

## 2. Architecture & Tech Stack

```
[ ShipmentWorkflow / Dispatch UI / Staff.Bff ]
                      │
                      ▼ (gRPC Port 5005)
┌─────────────────────────────────────────────────────────────┐
│                 RoutePlanningAgent Microservice             │
│  ├── VROOM / OSRM Multi-Stop Optimization Engine            │
│  ├── Composite Risk Assessment Engine (Score 0-100)         │
│  ├── Policy Provider & Tenant Risk Policy Config            │
│  ├── Staff vs. Manager Governance Workflow                  │
│  ├── Immutable Decision Audit Logger                        │
│  └── Transactional Outbox (RabbitMQ Event Publisher)        │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]        [ VROOM & OSRM Solver ]
     (Routes, Policies, Audits)        (Fast C++ Routing Engine)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Optimization Solvers**| VROOM (Vehicle Routing Open-source Optimization Machine), OSRM (Open Source Routing Machine) |
| **Persistence & ORM** | Entity Framework Core 10, PostgreSQL 16 (Neon Serverless SSL) |
| **AI Integration** | Central `AiGovernance` gRPC service (`capability: "route.plan"`) |
| **Messaging & Events** | Transactional Outbox Pattern, RabbitMQ (`RouteOptimizedEvent`, `RouteApprovedEvent`) |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`Routes` & `RouteStops`**: Waypoints, geographic coordinates (lat/lng), arrival/departure windows, sequence indices, ETA, and cargo load.
- **`RiskAssessments`**: Computed composite risk score $[0, 100]$, risk level (`Low`, `Medium`, `High`, `Critical`), breakdown factors (detour %, toll delta, weather hazard, hazmat constraint).
- **`TenantRiskPolicyConfigs`**: Tenant configuration pointing to `PolicyMode` (`Unconfigured`, `UsePlatformDefault`, `CustomPolicy`), `ActivePolicyId`, and `ActivePolicyVersion`.
- **`TenantRiskPolicies` & `Rules`**: Versioned custom risk policies with customizable weightings and approval thresholds.
- **`ApprovalRequests` & `RouteDecisionAuditLogs`**: Manager approval tickets and immutable decision audit trails.

---

## 4. API & Contract Surface

Exposed via `protos/route_planning.proto` (`RoutePlanningService`):
- `OptimizeRoute`: Solves optimal stop sequence and computes risk assessment.
- `EvaluateRouteRisk`: Runs risk assessment and governance decision on proposed manual route edits.
- `ApproveRoute`: Manager approval API requiring capability `route_planning:approve`.
- `RejectRoute`: Manager rejection API.
- `ConfigureRiskPolicy`: Tenant Admin API to configure risk thresholds and policy modes.

---

## 5. Security & Invariants

1. **Unconfigured Policy Invariant**: If a tenant's policy mode is `Unconfigured`, route operations throw `RiskPolicyNotConfiguredException` (fail-closed) rather than silently using defaults.
2. **Approval Authorization Gate**: Route approvals strictly require explicit capability `route_planning:approve`.
3. **Current Maturity**: Production-ready VROOM integration, risk governance engine, and tenant policy versioning with complete test coverage.
