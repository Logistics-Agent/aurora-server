# Aurora Platform - Blocked APIs (BLOCKED_BY_BACKEND_CONTRACT)

> **Document ID:** `DOC-BFF-BLOCKED`  
> **Status:** Canonical Gap Inventory  
> **Scope:** Business capabilities currently blocked from BFF implementation due to missing Protobuf contracts (`G2`), implementation gaps (`G3`), or service stub mismatches (`G4`).  
> **Anti-Hallucination Rule:** These endpoints MUST NOT be created or stubbed in BFF until backend engineering completes the required gRPC contracts and implementations.

---

## 1. Blocked API Summary Table

| Desired Endpoint | Roles | Business Function | Service | Current Capability | Missing Layer | Priority |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| `PUT /api/v1/system/tenants/{id}` | `[SYSTEM]` | Update Tenant Company Profile | `IamTenant` | `UpdateTenantCommand.cs` | Proto + gRPC implementation | **P1** |
| `POST /api/v1/invoices/{id}/payments` | `[STAFF, MANAGER]` | Record Customer Payment | `billing-service` | `billing.service.ts` | Proto declaration | **P1** |
| `POST /api/v1/invoices/{id}/cancel` | `[MANAGER, ADMIN]` | Cancel Unpaid Invoice | `billing-service` | `billing.service.ts` | Proto declaration | **P1** |
| `POST /api/v1/invoices/{id}/debit-notes`| `[MANAGER, ADMIN]` | Issue Debit Adjustment Note | `billing-service` | `billing.service.ts` | Proto declaration | **P1** |
| `POST /api/v1/invoices/{id}/credit-notes`| `[MANAGER, ADMIN]`| Issue Credit Refund Note | `billing-service` | `billing.service.ts` | Proto declaration | **P1** |
| `GET /api/v1/financial/exchange-rate` | `[STAFF, MANAGER, ADMIN]` | Query Currency Conversion Rate | `financial-service`| `financial.service.ts`| Proto declaration | **P2** |
| `POST /api/v1/negotiation/offer` | `[STAFF, MANAGER]` | Submit AI Freight Negotiation Bid| `negotiation-agent`| `negotiation.service.ts`| Proto declaration + file | **P1** |
| `GET /api/v1/negotiation/session/{id}` | `[STAFF, MANAGER, ADMIN]` | Get Negotiation Dialogue Log | `negotiation-agent`| `negotiation.service.ts`| Proto declaration + file | **P1** |

---

## 2. Granular Blocked Capability Specifications

### 1. `PUT /api/v1/system/tenants/{id}`
- **Desired Endpoint:** `PUT /api/v1/system/tenants/{id}`
- **Roles:** `[SYSTEM_ADMIN]`
- **Business Function:** Updates customer tenant profile, business name, tax code, and subscription plan tier.
- **Current Service:** `IamTenant`
- **Current Capability:** Fully implemented MediatR command and handler `UpdateTenantCommand.cs` & `UpdateTenantHandler`.
- **Missing:**
  - `[x] Proto contract` in `protos/iam_tenant.proto`
  - `[x] gRPC override` in `IamGrpcService.cs`
- **Files Requiring Backend Changes:**
  - [protos/iam_tenant.proto](file:///D:/IT/CD/aurora-server/protos/iam_tenant.proto)
  - [src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs)

---

### 2. `POST /api/v1/invoices/{id}/payments`
- **Desired Endpoint:** `POST /api/v1/invoices/{id}/payments`
- **Roles:** `[STAFF, MANAGER]`
- **Business Function:** Records customer payment against open invoice and updates status to `PAID` or `PARTIAL`.
- **Current Service:** `billing-service` (NestJS)
- **Current Capability:** Implemented in `billing.service.ts` (`recordPayment`).
- **Missing:**
  - `[x] Proto contract` in `protos/billing.proto`
- **Files Requiring Backend Changes:**
  - [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto)
  - [src/nestjs/billing-service/src/interface/controllers/billing.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/interface/controllers/billing.controller.ts)

---

### 3. `POST /api/v1/invoices/{id}/debit-notes` & `POST /api/v1/invoices/{id}/credit-notes`
- **Desired Endpoint:** `POST /api/v1/invoices/{id}/debit-notes`, `POST /api/v1/invoices/{id}/credit-notes`
- **Roles:** `[MANAGER, ADMIN]`
- **Business Function:** Issues post-invoicing financial adjustments and disputes.
- **Current Service:** `billing-service` (NestJS)
- **Current Capability:** Implemented in `billing.service.ts` (`issueDebitNote`, `issueCreditNote`).
- **Missing:**
  - `[x] Proto contract` in `protos/billing.proto`
- **Files Requiring Backend Changes:**
  - [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto)

---

### 4. `POST /api/v1/negotiation/offer` & `GET /api/v1/negotiation/session/{id}`
- **Desired Endpoint:** `POST /api/v1/negotiation/offer`, `GET /api/v1/negotiation/session/{id}`
- **Roles:** `[STAFF, MANAGER]`
- **Business Function:** Frontline rate negotiation desk with automated AI pricing counter-offers and speech generation.
- **Current Service:** `negotiation-agent-service` (NestJS)
- **Current Capability:** Implemented in `negotiation.service.ts` (`submitOffer`, `getSessionHistory`).
- **Missing:**
  - `[x] Proto file & contract` in `protos/negotiation.proto`
- **Files Requiring Backend Changes:**
  - `protos/negotiation.proto` (New file)
  - [src/nestjs/negotiation-agent-service/src/interface/controllers/negotiation.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/negotiation-agent-service/src/interface/controllers/negotiation.controller.ts)
