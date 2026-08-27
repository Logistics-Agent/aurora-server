# Intelligent Route Planning & Risk Governance — Deep Technical Details

> **Service Layer**: Optimization Algorithms, Risk Engine, Policy Modes & Governance  
> **Source-of-Truth**: `src/dotnet/RoutePlanningAgent`, `Route.cs`, `RiskAssessment.cs`, `TenantRiskPolicyConfig.cs`, `RoutePlanningAgentDbContext.cs`.

---

## 1. Mathematical Optimization Engine (VROOM + OSRM)

Aurora leverages **VROOM** (C++ optimization solver) connected to an **OSRM** backend:
1. **Distance Matrix Calculation**: OSRM computes shortest-path travel durations and distance matrices across all waypoints using OpenStreetMap road networks.
2. **CVRPTW Solving**: VROOM evaluates vehicle capacities (weight/volume), stop service times, time windows, and driver break requirements using heuristic local search (metaheuristics / Tabu search).
3. **Output**: Returns ordered stop sequences, exact ETAs, travel durations, and total distance.

---

## 2. Risk-Based Operational Governance Engine

Rather than forcing every route modification to wait in a manager approval queue, proposed routes pass through the **Risk Assessment & Governance Pipeline**:

```
Proposed Route / Modification
              │
              ▼
   [ Composite Risk Engine ]
   ├── Detour Ratio Factor (vs direct baseline)
   ├── Toll / Fuel Cost Delta Factor
   ├── Delivery Time Window Violation Margin
   ├── Hazardous Material / Urban Restriction
   └── Weather / Traffic Hazard Index
              │
              ▼ (Score: 0 - 100)
 ┌────────────┼────────────┬──────────────┐
 LOW        MEDIUM        HIGH          CRITICAL
 (0 - 25)   (26 - 50)    (51 - 75)      (76 - 100)
  │           │            │               │
  ▼           ▼            ▼               ▼
Auto-Ready  Staff Review Manager Approval Hard Block
```

### 2.1 Governance Decision Mapping:
- **`LOW` Risk**: Status becomes `Ready` / `AutoApproved`. Dispatched immediately.
- **`MEDIUM` Risk**: Operational staff can review warnings and confirm dispatch.
- **`HIGH` Risk**: Generates an `ApprovalRequest` ticket. Dispatch is paused until an authorized manager (`route_planning:approve`) reviews and approves.
- **`CRITICAL` Risk**: Marked `Blocked`. Disallowed entirely due to severe safety or legal violation (e.g. hazardous material in prohibited tunnel).

---

## 3. Tenant Risk Policy Architecture & Versioning

AI configuration was removed from RoutePlanning and migrated to `AiGovernance`. RoutePlanning owns strictly **Tenant Risk Policies**:

```csharp
public class TenantRiskPolicyConfig : TenantAuditableEntity
{
    public PolicyMode PolicyMode { get; set; } // Unconfigured | UsePlatformDefault | CustomPolicy
    public Guid? ActivePolicyId { get; set; }
    public int ActivePolicyVersion { get; set; } = 1;
}
```

### 3.1 Policy Resolution Logic:
1. **`PolicyMode.Unconfigured`**: Throws `RiskPolicyNotConfiguredException` (fail-closed security).
2. **`PolicyMode.UsePlatformDefault`**: Evaluates against built-in platform baseline thresholds.
3. **`PolicyMode.CustomPolicy`**: Loads the exact immutable `TenantRiskPolicy` matching `ActivePolicyId` and `ActivePolicyVersion`.

---

## 4. Concurrency & Idempotency

- **Optimistic Concurrency**: `Route.Version` token prevents conflicting simultaneous stop additions.
- **Decision Auditing**: Every transition writes a `RouteDecisionAuditLog` record linking `ActorId`, `RiskScore`, `PreviousStatus`, and `NewStatus`.
- **Transactional Outbox**: Emits `RouteOptimizedEvent` and `RouteApprovedEvent` to RabbitMQ.
