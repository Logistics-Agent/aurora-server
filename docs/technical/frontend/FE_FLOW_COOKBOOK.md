# Aurora Server — Frontend Flow Cookbook & UI State Recipes

> **Authoritative Playbook**: Step-by-step integration flows for frontend developers with concrete JSON request/response payloads, UI state machine transitions, WebSocket reactions, and capability permission checks.

---

## 1. Authentication & Bootstrap Flow

```
[Unauthenticated SPA] 
  ──> Click "Login" 
  ──> Redirect to /api/v1/auth/login?returnUrl=/dashboard 
  ──> Cognito Login Page 
  ──> Auth Code Exchange 
  ──> Set Cookie (.AspNetCore.Cookies) 
  ──> SPA Mount 
  ──> GET /api/v1/auth/me 
  ──> Initialize RealtimeHub WebSocket
```

### Step 1: Initiating Login
```typescript
function handleLoginRedirect() {
  const currentPath = window.location.pathname;
  window.location.href = `/api/v1/auth/login?returnUrl=${encodeURIComponent(currentPath)}`;
}
```

### Step 2: Bootstrapping on SPA Mount (`GET /api/v1/auth/me`)
- **HTTP Request**: `GET /api/v1/auth/me` (Credentials: `include`)
- **HTTP Response (200 OK)**:
  ```json
  {
    "email": "operator.vn@acmelogistics.com",
    "emailDomain": "acmelogistics.com",
    "cognitoSub": "4f1a23b4-5678-4321-8765-abcdef123456",
    "userId": "9a3c7e81-7788-4221-9988-112233445566",
    "tenantId": "e5b8ba84-0000-0000-0000-000000000001",
    "name": "Nguyen Van A",
    "role": "STAFF",
    "permissions": [
      "mail:read",
      "mail:draft:create",
      "mail:send",
      "mail:thread:claim",
      "shipments:read",
      "shipments:create",
      "shipments:update",
      "shipments:submit",
      "route_planning:read",
      "route_planning:create",
      "route_planning:update",
      "route_planning:optimize",
      "route_planning:execute",
      "financial_tax:read",
      "financial_tax:calculate",
      "billing_settlement:read",
      "billing_settlement:credit:check",
      "billing_settlement:escrow:read",
      "iam:user:read"
    ],
    "isAuthenticated": true
  }
  ```
- **UI State Transition**: Store `currentUser` and `permissions` in global auth state; initialize `RealtimeHub` WebSocket and join rooms `tenant:e5b8...` and `user:9a3c...`.

---

## 2. Shared Mail Inbox & Thread Triage Flow

### 2.1 Viewing the Triage Queues
1. **Unassigned Queue**:
   - `GET /api/v1/mail/threads?folder=UNASSIGNED&page=1&pageSize=20`
   - Shows incoming unassigned emails. "Claim" button enabled for users with `hasPermission('mail:thread:claim')`.
2. **My Work Queue**:
   - `GET /api/v1/mail/threads?folder=MY_WORK&page=1&pageSize=20`
   - Shows threads assigned to `currentUser.userId`. "Reply", "Draft", "Send" enabled for users with `mail:draft:create` and `mail:send`.
3. **All Queue (Supervisor / Manager View)**:
   - `GET /api/v1/mail/threads?folder=ALL&page=1&pageSize=20`
   - Requires `mail:thread:read_all`. Shows all staff assignments with "Reassign" (`mail:thread:reassign`) and "Unassign" (`mail:thread:unassign`) actions.

### 2.2 Claiming an Unassigned Thread
- **User Action**: Staff clicks **"Claim Thread"** on thread `3fa85f64-5717-4562-b3fc-2c963f66afa6`.
- **Permission Check**: `<PermissionGate permission="mail:thread:claim">`
- **HTTP Request**: `POST /api/v1/mail/threads/3fa85f64-5717-4562-b3fc-2c963f66afa6/claim`
- **HTTP Response (200 OK)**:
  ```json
  {
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "subject": "Urgent Freight Quotation - Ho Chi Minh to Singapore",
    "status": "ASSIGNED",
    "assignedUserId": "9a3c7e81-7788-4221-9988-112233445566",
    "assignedStaffName": "Nguyen Van A",
    "lastMessageAt": "2026-08-27T08:30:00Z"
  }
  ```
- **Realtime Reaction**: All other connected staff receive WebSocket event `THREAD_CLAIMED`. Their UI updates to gray out the thread or remove it from the Unassigned view.

### 2.3 Creating a Draft & Sending Outbound Reply
- **Step 1: Save Draft**: `POST /api/v1/mail/drafts` (`mail:draft:create`)
  ```json
  {
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "recipientEmails": ["shipper@clientcorp.com"],
    "subject": "Re: Urgent Freight Quotation - Ho Chi Minh to Singapore",
    "bodyHtml": "<p>Hello, please find our official quotation of $1,450 USD attached.</p>",
    "bodyText": "Hello, please find our official quotation of $1,450 USD attached."
  }
  ```
- **Step 2: Send Message**: `POST /api/v1/mail/messages/outbound` (`mail:send`)
  ```json
  {
    "draftId": "draft-1092-...",
    "senderEmail": "operator.vn@acmelogistics.com",
    "recipientEmails": ["shipper@clientcorp.com"],
    "subject": "Re: Urgent Freight Quotation - Ho Chi Minh to Singapore",
    "bodyHtml": "<p>Hello, please find our official quotation of $1,450 USD attached.</p>",
    "bodyText": "Hello, please find our official quotation of $1,450 USD attached."
  }
  ```
- **HTTP Response (200 OK)**:
  ```json
  {
    "messageId": "msg-9921-...",
    "status": "QUEUED_FOR_DELIVERY",
    "stalwartQueueId": "stalwart-q-7819"
  }
  ```

---

## 3. Negotiation to Email Draft Flow

```
[Inbound Thread with Shipper Offer]
  ──> User clicks "Generate AI Counter-Offer"
  ──> POST /api/v1/negotiations/{negotiationId}/mail-draft
  ──> AI evaluates Concession Curve + Floor Price
  ──> Creates Draft in MailService
  ──> UI redirects to Draft Editor for human review & send
```

- **HTTP Request**: `POST /api/v1/negotiations/neg-4401/mail-draft`
  ```json
  {
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "targetMarginPercent": 12.5,
    "strategy": "BALANCED_CONCESSION"
  }
  ```
- **HTTP Response (200 OK)**:
  ```json
  {
    "draftId": "draft-8812-...",
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "suggestedPrice": 1380.00,
    "currency": "USD",
    "subject": "Re: Counter Offer - Freight Route SG-VN",
    "bodyHtml": "<p>Thank you for your proposal. We can accommodate your shipment at a revised rate of <strong>$1,380 USD</strong> including port handling fees...</p>",
    "explanation": "Concession of $70 granted from initial $1,450 quote. Profit margin remains above the required 10% floor ($1,210 cost base)."
  }
  ```
- **UI State Transition**: Load generated content into rich text email editor; highlight suggested counter-rate; staff clicks "Send" (`mail:send`).

---

## 4. End-to-End Shipment Lifecycle Flow

```
1. Create Shipment (DRAFT)
2. Attach Documents (BoL / Invoice) ──> Auto-trigger OCR
3. Submit Shipment (SUBMITTED)     ──> Auto-trigger Compliance Check
4. Dispatch / Assign Route (BOOKED / IN_TRANSIT)
5. Driver In-Transit Updates        ──> GPS Telematics & Geofences
6. Delivery & POD Upload           ──> Transitions to COMPLETED
7. Automated Invoicing              ──> billing-service generates Invoice
```

### 4.1 Step 1: Create Shipment (`shipments:create`)
- **HTTP Request**: `POST /api/v1/shipments`
  ```json
  {
    "orderId": "PO-2026-8831",
    "customerName": "Samsung Electronics Vietnam",
    "originAddress": "Yen Phong Industrial Zone, Bac Ninh, Vietnam",
    "destinationAddress": "Tanjung Pelepas Port, Malaysia",
    "originCountry": "VN",
    "destinationCountry": "MY",
    "cargoItems": [
      {
        "name": "OLED Display Panels Module",
        "quantity": 1200,
        "weightKg": 3600.0,
        "hsCode": "8528.59.00"
      }
    ]
  }
  ```
- **HTTP Response (201 Created)**: Returns `ShipmentDto` with `id: 7fa85f64-...`, `status: DRAFT`.

### 4.2 Step 2: Attach Document (`documents:ingest`)
- **HTTP Request**: `POST /api/v1/shipments/7fa85f64-.../documents`
  ```json
  {
    "fileName": "Commercial_Invoice_Samsung_8831.pdf",
    "documentType": "COMMERCIAL_INVOICE",
    "storageUrl": "r2://shipment-docs/e5b8/INV8831.pdf",
    "ocrStatus": "PROCESSING"
  }
  ```
- **Realtime Reaction**: Document OCR worker starts. When finished, WebSocket event `DOCUMENT_OCR_COMPLETED` is received:
  ```json
  {
    "shipmentId": "7fa85f64-...",
    "documentId": "doc-1122-...",
    "ocrConfidence": 0.98,
    "needsReview": false
  }
  ```

### 4.3 Step 3: Submit Shipment (`shipments:submit`)
- **HTTP Request**: `POST /api/v1/shipments/7fa85f64-.../submit`
- **HTTP Response (200 OK)**: `ShipmentDto` (`status: SUBMITTED`).
- **Realtime Reaction**: Compliance evaluation runs automatically in background and updates compliance badge to `PASSED` or `WARNINGS`.

---

## 5. Document OCR Review & Correction Flow

```
[Low-Confidence OCR (< 0.85)] 
  ──> Status: NEEDS_REVIEW 
  ──> User with ocr:review views OCR Diff Viewer 
  ──> Corrects field values 
  ──> POST /api/v1/documents/ocr/jobs/{id}/review 
  ──> Status: READY
```

- **Permission Check**: `<PermissionGate permission="ocr:review">`
- **HTTP Request**: `POST /api/v1/documents/ocr/jobs/ocr-job-5501/review`
  ```json
  {
    "action": "CORRECT",
    "fields": [
      { "name": "TaxId", "value": "0100109106" },
      { "name": "TotalAmount", "value": "78450.00" }
    ],
    "comment": "Overrode optical artifact on invoice total line."
  }
  ```
- **HTTP Response (200 OK)**: `UnifiedDocumentStatusResponse` (`status: READY`, `needsReview: false`).

---

## 6. Route Planning & Risk-Based Governance Flow

```
1. POST /api/v1/routes (Create Route - route_planning:create)
2. POST /api/v1/routes/{id}/optimize (Execute VROOM Solver - route_planning:optimize)
3. Evaluates Risk Engine:
   ├─ LOW / MEDIUM ──> Staff clicks "Execute Route" (Direct Dispatch - route_planning:execute)
   └─ HIGH RISK    ──> Route marked "PENDING_APPROVAL"
                        ──> Approver with route_planning:approve visits /approvals/routes
                        ──> POST /api/v1/approvals/routes/{id}/approve
                        ──> Route marked "APPROVED" / "READY"
```

- **Step 1: Optimize Route**: `POST /api/v1/routes/rt-7712/optimize`
  ```json
  {
    "trafficModel": "BEST_GUESS",
    "maxDriverHours": 8,
    "avoidTolls": false
  }
  ```
- **Response**: If high-risk rules trigger (e.g. `HeavyWeightRule` + `LongDurationRule` > 12 hours), `riskLevel: HIGH`, `governanceDecision: ManagerApprovalRequired`.
- **Step 2: Approval by Authorized Approver**: `POST /api/v1/approvals/routes/rt-7712/approve`
  - **Permission Check**: `<PermissionGate permission="route_planning:approve">`
  ```json
  {
    "comment": "Approved with dual-driver rotation requirement."
  }
  ```
- **Response**: `ApprovalResponse` (`status: APPROVED`).

---

## 7. GPS Real-time Telematics & Geofence Alert Flow

### 7.1 Displaying Live Tracking
- **HTTP Request**: `GET /api/v1/tracking/7fa85f64-.../current?type=shipment`
- **HTTP Response (200 OK)**:
  ```json
  {
    "shipmentId": "7fa85f64-...",
    "vehicleId": "59C-998.12",
    "latitude": 10.8231,
    "longitude": 106.6297,
    "speedKph": 52.0,
    "headingDegrees": 140.0,
    "recordedAt": "2026-08-27T08:50:00Z"
  }
  ```
- **Realtime Reaction**: SPA listens on WebSocket for `GPS_POSITION_UPDATED` and smoothly animates the vehicle icon on Mapbox / Google Maps.

### 7.2 Creating a Geofence (`gps_tracking:geofence:manage`)
- **HTTP Request**: `POST /api/v1/tracking/geofences`
  ```json
  {
    "name": "Tan Son Nhat Air Cargo Gate 3",
    "latitude": 10.8180,
    "longitude": 106.6600,
    "radiusMeters": 350.0,
    "shipmentId": "7fa85f64-..."
  }
  ```

---

## 8. Financial Estimation & POD Escrow Release Flow

### 8.1 Estimating Freight Cost (`financial_tax:calculate`)
- **HTTP Request**: `POST /api/v1/financial/estimate-cost`
  ```json
  {
    "originPort": "SGSIN",
    "destinationPort": "VNSGN",
    "weightKg": 4500.0,
    "volumeCbm": 12.0,
    "transportMode": "SEA",
    "currency": "USD"
  }
  ```
- **HTTP Response (200 OK)**:
  ```json
  {
    "baseRate": 1200.00,
    "fuelSurcharge": 150.00,
    "portHandlingCharge": 100.00,
    "totalEstimatedCost": 1450.00,
    "currency": "USD"
  }
  ```

### 8.2 Releasing Escrow Settlement upon POD Delivery (`billing_settlement:settlement:manage`)
- **Permission Check**: `<PermissionGate permission="billing_settlement:settlement:manage">`
- **HTTP Request**: `POST /api/v1/billing/escrow/release`
  ```json
  {
    "walletId": "wlt-escrow-501",
    "transactionId": "tx-escrow-Samsung8831",
    "amount": 1450.00,
    "currency": "USD",
    "note": "Signed Proof of Delivery verified by customs coordinator."
  }
  ```
- **HTTP Response (200 OK)**:
  ```json
  {
    "transactionId": "tx-release-9901",
    "status": "SETTLED",
    "releasedAmount": 1450.00,
    "remainingWalletBalance": 0.00,
    "settledAt": "2026-08-27T09:00:00Z"
  }
  ```
