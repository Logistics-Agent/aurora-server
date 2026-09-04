# Aurora Platform — Operations Workspace API Catalog (Staff Execution)

> **Document ID:** `DOC-BFF-STAFF`  
> **Status:** Canonical Specification (Synchronized with `Staff.Bff` C# Source)  
> **Scope:** HTTP REST APIs consumed by the **Aurora Operations Workspace** for operational execution (`Staff.Bff`).  
> **Base Controller:** `[Authorize]` via [StaffControllerBase.cs](file:///d:/IT/CD/aurora-server/src/dotnet/BFF/Staff.Bff/Controllers/StaffControllerBase.cs).  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Operational Endpoints Table

| Module | Method | Path | Purpose | Permission | Scope | Backend RPC | Status |
|---|---|---|---|---|---|---|:---:|
| **Shipment** | `POST` | `/api/v1/shipments` | Create draft shipment | `shipments:create` | Tenant | `ShipmentWorkflowService.CreateShipment` | `CURRENT` |
| **Shipment** | `GET` | `/api/v1/shipments` | List shipments with filters | `shipments:read` | Tenant | `ShipmentWorkflowService.ListShipments` | `CURRENT` |
| **Shipment** | `GET` | `/api/v1/shipments/{id}` | Get shipment details | `shipments:read` | Tenant | `ShipmentWorkflowService.GetShipment` | `CURRENT` |
| **Shipment** | `PUT` | `/api/v1/shipments/{id}` | Update shipment draft | `shipments:update` | Tenant | `ShipmentWorkflowService.UpdateShipment` | `CURRENT` |
| **Shipment** | `DELETE`| `/api/v1/shipments/{id}` | Delete shipment draft | `shipments:delete` | Tenant | `ShipmentWorkflowService.DeleteShipment` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/submit` | Submit shipment for execution | `shipments:submit` | Tenant | `ShipmentWorkflowService.SubmitShipment` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/cancel` | Cancel active shipment | `shipments:cancel` | Tenant | `ShipmentWorkflowService.CancelShipment` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/milestones` | Record delivery milestone | `shipments:milestones:update` | Tenant | `ShipmentWorkflowService.UpdateMilestone` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/documents` | Attach OCR document | `documents:attach` | Tenant | `ShipmentWorkflowService.AttachDocument` | `CURRENT` |
| **Shipment** | `GET` | `/api/v1/shipments/{id}/events` | Get shipment event audit trail | `shipments:read` | Tenant | `ShipmentWorkflowService.GetShipmentEvents` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes` | Create route proposal | `route_planning:create` | Tenant | `RoutePlanningService.CreateRoute` | `CURRENT` |
| **Routes** | `GET` | `/api/v1/routes` | List routes with pagination | `route_planning:read` | Tenant | `RoutePlanningService.ListRoutes` | `CURRENT` |
| **Routes** | `GET` | `/api/v1/routes/{id}` | Get route details & stops | `route_planning:read` | Tenant | `RoutePlanningService.GetRoute` | `CURRENT` |
| **Routes** | `PUT` | `/api/v1/routes/{id}` | Update route parameters | `route_planning:update` | Tenant | `RoutePlanningService.UpdateRoute` | `CURRENT` |
| **Routes** | `DELETE`| `/api/v1/routes/{id}` | Delete draft route | `route_planning:delete` | Tenant | `RoutePlanningService.DeleteRoute` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/stops` | Add stop to route | `route_planning:update` | Tenant | `RoutePlanningService.AddStop` | `CURRENT` |
| **Routes** | `DELETE`| `/api/v1/routes/{id}/stops/{stopId}` | Remove stop from route | `route_planning:update` | Tenant | `RoutePlanningService.RemoveStop` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/optimize` | Trigger AI route optimization | `route_planning:optimize` | Tenant | `RoutePlanningService.OptimizeRoute` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/evaluate-risk` | Evaluate route risk score | `route_planning:risk:evaluate` | Tenant | `RoutePlanningService.EvaluateRisk` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/dispatch` | Dispatch approved route | `route_planning:dispatch` | Tenant | `RoutePlanningService.DispatchRoute` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/threads` | List email threads (`UNASSIGNED` / `MY_WORK`) | `mail:read` | Mailbox/User | `MailSecurity.ListThreads` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/threads/{id}` | Get thread conversation & history | `mail:read` | Mailbox/User | `MailSecurity.GetThread` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/threads/{id}/claim` | Atomically claim thread (Take Thread) | `mail:thread:claim` | Thread Assignee | `MailSecurity.ClaimThread` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/threads/{id}/assignment-history` | View thread ownership transitions | `mail:read` | Thread | `MailSecurity.GetThreadAssignmentHistory` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/drafts` | Create or update email draft | `mail:draft:create` | Mailbox/User | `MailSecurity.CreateDraftMessage` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/drafts` | List drafts for mailbox | `mail:read` | Mailbox/User | `MailSecurity.ListDrafts` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/drafts/{id}` | Get draft revision content | `mail:read` | Mailbox/User | `MailSecurity.GetDraft` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/messages/outbound` | Submit outbound email | `mail:send` | Mailbox | `MailSecurity.SubmitOutboundMessage` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/messages` | List processed messages | `mail:read` | Mailbox | `MailSecurity.ListProcessedMessages` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/messages/{id}` | Get processed message & security checks | `mail:read` | Mailbox | `MailSecurity.GetProcessedMessage` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/quarantine` | List quarantined emails | `mail:quarantine:read` | Tenant | `MailSecurity.ListQuarantineRecords` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/quarantine/{id}` | Inspect quarantined threat record | `mail:quarantine:read` | Tenant | `MailSecurity.GetQuarantineRecord` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/quarantine/{id}/release` | Release false-positive email to queue | `mail:quarantine:release` | Tenant | `MailSecurity.ReleaseQuarantine` | `CURRENT` |
| **OCR** | `POST` | `/api/v1/documents/shipment` | Submit document for structured extraction | `shipments:create` | Shipment | `DocumentOcrService.SubmitOcrJob` | `CURRENT` |
| **OCR** | `GET` | `/api/v1/documents/jobs/{jobId}` | Get OCR job status | `shipments:read` | Job | `DocumentOcrService.GetOcrJob` | `CURRENT` |
| **OCR** | `GET` | `/api/v1/documents/jobs/{jobId}/ocr-result` | Get extracted JSON payload | `shipments:read` | Job | `DocumentOcrService.GetOcrResult` | `CURRENT` |
| **OCR** | `POST` | `/api/v1/documents/jobs/{jobId}/review` | Human-in-the-loop review confirmation | `documents:review` | Job | `DocumentOcrService.ReviewOcrJob` | `CURRENT` |
| **Compliance** | `POST` | `/api/v1/compliance/evaluations` | Evaluate shipment trade compliance | `compliance:evaluate` | Shipment | `RegulatoryComplianceService.EvaluateCompliance` | `CURRENT` |
| **Compliance** | `GET` | `/api/v1/compliance/evaluations/{id}` | Get compliance assessment result | `compliance:read` | Evaluation | `RegulatoryComplianceService.GetEvaluation` | `CURRENT` |
| **Compliance** | `POST` | `/api/v1/compliance/rag/query` | Query regulatory citations | `compliance:read` | Jurisdiction | `RegulatoryComplianceService.QueryRegulations` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/{id}/current` | Get latest GPS position | `shipments:read` | Shipment/Vehicle | `GpsTrackingService.GetCurrentLocation` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/{id}/history` | Get GPS breadcrumb history | `shipments:read` | Shipment/Vehicle | `GpsTrackingService.ListPositionHistory` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/{id}/alerts` | Get geofence & sensor alerts | `shipments:read` | Shipment/Vehicle | `GpsTrackingService.ListAlerts` | `CURRENT` |
| **Tracking** | `POST` | `/api/v1/tracking/geofences` | Create geofence monitoring zone | `shipments:update` | Tenant | `GpsTrackingService.CreateGeofence` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/geofences` | List tenant geofences | `shipments:read` | Tenant | `GpsTrackingService.ListGeofences` | `CURRENT` |
| **Financial** | `POST` | `/api/v1/financial/estimate-cost` | Calculate freight cost estimate | `financial:calculate` | Tenant | `FinancialService.EstimateCost` | `CURRENT` |
| **Financial** | `POST` | `/api/v1/financial/customs-duty` | Calculate customs duties & tariffs | `financial:calculate` | Tenant | `FinancialService.GetCustomsDuty` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/invoices/generate` | Auto-generate shipment invoice | `billing:invoice:create` | Shipment | `BillingService.GenerateInvoice` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/invoices` | Create manual invoice | `billing:invoice:create` | Customer | `BillingService.CreateInvoice` | `CURRENT` |
| **Billing** | `GET` | `/api/v1/invoices/{id}` | Get invoice details | `billing:invoice:read` | Invoice | `BillingService.GetInvoice` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/invoices/{id}/pay` | Record invoice settlement | `billing:invoice:pay` | Invoice | `BillingService.PayInvoice` | `CURRENT` |
| **Billing** | `GET` | `/api/v1/billing/credit-check` | Check customer credit limit | `billing:credit:read` | Customer | `BillingService.CheckCreditLimit` | `CURRENT` |
| **Billing** | `GET` | `/api/v1/escrow/accounts/{customerId}` | Check escrow balance | `billing:escrow:read` | Customer | `BillingService.GetEscrowAccount` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/escrow/lock` | Lock funds in escrow | `billing:escrow:manage` | Customer | `BillingService.LockEscrow` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/escrow/release` | Release locked escrow funds | `billing:escrow:manage` | Customer | `BillingService.ReleaseEscrow` | `CURRENT` |
| **Negotiation**| `POST` | `/api/v1/negotiations/{id}/mail-draft` | Generate draft from AI suggestion | `mail:draft:create` | Negotiation | `NegotiationService.GetDraftSuggestion` + `MailSecurity.CreateDraftMessage` | `CURRENT` |
| **Negotiation**| `GET` | `/api/v1/negotiations/{id}/suggestion` | View AI concession counter-offer | `mail:read` | Negotiation | `NegotiationService.GetDraftSuggestion` | `CURRENT` |
| **Assistant** | `POST` | `/api/v1/assistant/query` | Grounded multi-corpus AI assistant | `compliance:read` | Tenant | `RegulatoryComplianceService.GenerateGroundedAnswer` | `CURRENT` |
| **Chat** | `POST` | `/api/v1/chat/conversations` | Customer Assistant conversation | `None (Session)` | Tenant | `CustomerAssistantService (HTTP)` | `CURRENT` |

---

## 2. Mail Thread Operational Workflow

```text
[Incoming Email Ingestion]
        ↓
[UNASSIGNED Queue] (scope=UNASSIGNED)
        ↓ Staff clicks "Take Thread"
[POST /api/v1/mail/threads/{id}/claim]
        ├── Enforces Optimistic Concurrency (thread.Version)
        ├── Sets PrimaryAssigneeUserId = currentUser.UserId
        └── Transitions Status = IN_PROGRESS
        ↓
[MY_WORK Queue] (scope=MY_WORK)
        ├── Compose Draft: POST /api/v1/mail/drafts
        └── Submit Outbound: POST /api/v1/mail/messages/outbound
```
