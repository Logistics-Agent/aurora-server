# Aurora Platform - Manager Exclusive API Catalog (MANAGER_ONLY)

> **Document ID:** `DOC-BFF-MGR`  
> **Status:** Canonical Specification Complete  
> **Scope:** HTTP REST APIs exclusively accessible by the `MANAGER` role (Supervisory dual-control authorization gates).  
> **Rule:** Shared supervisory APIs (e.g. pending approval queue list, alert resolution, quarantine release) are defined in [shared-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/shared-api.md).

---

## 1. Manager Exclusive API Table

| Method | Endpoint | Function | Service | RPC | Main Source File |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/approvals/{id}/approve` | Approve AI Route Recommendation | `RoutePlanningAgent` | `RoutePlanningService.ApproveRoute` | `Staff.Bff/Controllers/ApprovalsController.cs` |
| `POST` | `/api/v1/approvals/{id}/reject` | Reject AI Route Recommendation | `RoutePlanningAgent` | `RoutePlanningService.RejectRoute` | `Staff.Bff/Controllers/ApprovalsController.cs` |

---

## 2. API Specifications

### `POST /api/v1/approvals/{id}/approve`

- **Function:** Authorizes a high-risk or hazardous route proposal recommended by the AI Route Planning Agent, unblocking it for active dispatch.
- **Role:** `MANAGER_ONLY`
- **Tenant Scope:** Strict Tenant Isolation (`ICurrentUserService.TenantId`)
- **Backend Service:** `RoutePlanningAgent`
- **RPC:** `RoutePlanningService.ApproveRoute`
- **Request:**
  ```json
  {
    "comment": "Approved following safety compliance review"
  }
  ```
- **Response:**
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "routeId": "4ba85f64-5717-4562-b3fc-2c963f66afa6",
    "routeName": "Express North-South Corridor",
    "status": "APPROVED",
    "reason": "Route approved by supervisor",
    "aiSummary": "Optimized waypoint sequencing with valid driving hour rest stops",
    "complianceSummary": "All hazardous cargo permits verified",
    "rejectionReason": "",
    "createdAt": "2026-08-24T12:00:00.000Z"
  }
  ```
- **Source Flow:**
  ```text
  BFF (POST /api/v1/approvals/{id}/approve)
      -> GrpcClient (RoutePlanningService.ApproveRouteAsync)
      -> RPC (ApproveRoute)
      -> Command (ApproveRouteCommand)
      -> Handler (ApproveRouteCommandHandler)
      -> RoutePlanningDbContext (Updates ApprovalRequest to APPROVED, unblocks Route status)
  ```
- **Backend Files:**
  - Proto: [protos/route-planning-agent.proto](file:///D:/IT/CD/aurora-server/protos/route-planning-agent.proto)
  - gRPC Implementation: [src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs)
  - Command: [src/dotnet/RoutePlanningAgent/Application/Commands/Routes/ApproveRouteCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/Application/Commands/Routes/ApproveRouteCommand.cs)
  - Handler: `ApproveRouteCommandHandler`
  - Persistence: `RoutePlanningDbContext`
- **BFF Files:**
  - Controller: [src/dotnet/BFF/Staff.Bff/Controllers/ApprovalsController.cs](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/Staff.Bff/Controllers/ApprovalsController.cs)
  - Grpc Client: `RoutePlanningService.RoutePlanningServiceClient`
  - Authorization: `[Authorize]` with Manager permission check
- **Status:** `READY` (G0)

---

### `POST /api/v1/approvals/{id}/reject`

- **Function:** Rejects a pending route proposal with a mandatory business justification reason, preventing dispatch.
- **Role:** `MANAGER_ONLY`
- **Tenant Scope:** Strict Tenant Isolation (`ICurrentUserService.TenantId`)
- **Backend Service:** `RoutePlanningAgent`
- **RPC:** `RoutePlanningService.RejectRoute`
- **Request:**
  ```json
  {
    "reason": "Weather conditions exceed safety thresholds",
    "comment": "Reroute via inland highway required"
  }
  ```
- **Response:**
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "routeId": "4ba85f64-5717-4562-b3fc-2c963f66afa6",
    "routeName": "Express North-South Corridor",
    "status": "REJECTED",
    "reason": "Weather conditions exceed safety thresholds",
    "aiSummary": "Optimized waypoint sequencing",
    "complianceSummary": "",
    "rejectionReason": "Weather conditions exceed safety thresholds",
    "createdAt": "2026-08-24T12:00:00.000Z"
  }
  ```
- **Source Flow:**
  ```text
  BFF (POST /api/v1/approvals/{id}/reject)
      -> GrpcClient (RoutePlanningService.RejectRouteAsync)
      -> RPC (RejectRoute)
      -> Command (RejectRouteCommand)
      -> Handler (RejectRouteCommandHandler)
      -> RoutePlanningDbContext (Updates ApprovalRequest to REJECTED)
  ```
- **Backend Files:**
  - Proto: [protos/route-planning-agent.proto](file:///D:/IT/CD/aurora-server/protos/route-planning-agent.proto)
  - gRPC Implementation: [src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs)
  - Command: [src/dotnet/RoutePlanningAgent/Application/Commands/Routes/RejectRouteCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/Application/Commands/Routes/RejectRouteCommand.cs)
  - Handler: `RejectRouteCommandHandler`
- **BFF Files:**
  - Controller: [src/dotnet/BFF/Staff.Bff/Controllers/ApprovalsController.cs](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/Staff.Bff/Controllers/ApprovalsController.cs)
- **Status:** `READY` (G0)
