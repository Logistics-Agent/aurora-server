# Aurora Logistics Platform — Public Website Product & Content Specification

> **Design Target:** Figma AI / Figma Make Public Website Specification  
> **Source of Truth:** Audited against `.NET 10` Microservices, `Staff.Bff`, `Admin.Bff`, `ai-governance`, `NestJS` services, `PermissionConstants.cs`, and `docs/technical/**`.

---

## 1. Executive Summary & Brand Positioning

**Aurora** is an enterprise logistics and supply-chain execution platform that unifies freight operations, secure business communication, deterministic optimization, compliance workflows, real-time visibility, and governed AI automation.

### Positioning Invariant:
Aurora is **not** an experimental AI chatbot, a consumer last-mile courier app, or a passive TMS dashboard. Aurora is the **operational nervous system** for international freight forwarders, customs brokers, 3PLs, and global manufacturers—orchestrating complex freight movements across ports, airports, container terminals, and distribution hubs with mathematical precision and auditable human governance.

### Core Value Proposition:
> **"Unify your freight operations from customer email to carrier settlement—powered by deterministic optimization and governed AI."**

---

## 2. Target Audience & Buyer Personas

### Target Organizations:
- **International Freight Forwarders** (Air, Ocean, Multimodal)
- **Licensed Customs Brokers & Trade Compliance Agencies**
- **Third-Party Logistics (3PL) & Intermodal Transport Operators**
- **Industrial Manufacturers & Global Exporters/Importers**
- **Port Logistics Operators & Container Yard Hubs**

### Primary Buyer & Influencer Personas:
1. **Chief Operating Officer (COO) / VP of Global Logistics**: Seeks operational throughput, error reduction in cross-departmental handoffs, and scalable multi-tenant execution.
2. **Operations Director / Freight Operations Lead**: Wants unified email thread triage, automated document extraction, single-click VRP route optimization, and SLA compliance.
3. **Head of Customs & Trade Compliance**: Requires grounded legal citations, automated sanction checks, and auditable exception logging.
4. **Chief Technology Officer (CTO) / Enterprise Architect**: Demands tenant isolation, microservices scalability, capability-based access control (CBAC), and zero-hallucination AI guardrails.

---

## 3. The Central Customer Story: Connected Freight Execution

Aurora eliminates operational silos by connecting every stage of the international freight lifecycle into an unbroken execution pipeline:

```text
1. INTAKE          Customer inquiries, booking requests, and RFQs arrive at shared mailboxes (ops@, pricing@).
   │
2. TRIAGE          Incoming mail forms an EmailThread; AI screens for threats while staff claims operational ownership.
   │
3. DOCUMENTS       Shippers attach Bill of Lading, Invoice, and Packing List; DocumentOcr extracts structured data.
   │
4. COMPLIANCE      RegulatoryCompliance validates cargo against national trade laws with verified legal citations.
   │
5. OPTIMIZATION    RoutePlanningAgent runs VROOM/OSRM to build capacity-constrained, multi-stop route plans.
   │
6. GOVERNANCE      High-risk routes automatically trigger supervisor approval workflows (Human-in-the-Loop).
   │
7. DISPATCH & GPS  Shipment dispatches; GpsTracking streams live telemetry, geofence triggers, and ETA updates.
   │
8. SETTLEMENT      Delivery confirmed via POD; FinancialService computes duties, generates invoices, and releases escrow.
```

---

## 4. Current Platform Capabilities

### 1. Shipment Lifecycle Management
- Multi-modal shipment state machine (`Draft` → `Booked` → `Dispatched` → `InTransit` → `Delivered`).
- Cargo itemization (weight, volume, hazardous materials, temperature constraints).
- Milestone tracking with verifiable delivery timestamps.

### 2. Intelligent Business Mail
- Shared departmental mailboxes (`ops@acmelogistics.com`, `customs@acmelogistics.com`) replacing personal inbox silos.
- Single-assignee responsibility model (`PrimaryAssigneeUserId`) prevents duplicate replies.
- Multi-layer security pipeline (SPF/DKIM/DMARC checks, ClamAV antivirus, SpamAssassin, AI phishing detection).
- Traceable outbound attribution recording human author (`SentByUserId`).

### 3. Document Intelligence & OCR
- Automated extraction for Bills of Lading (B/L), Commercial Invoices, Packing Lists, and Customs Declarations.
- Multimodal layout analysis extracting container numbers, HS codes, declared values, and consignee details.
- Integrated human review queue (`ocr:review`) for low-confidence fields.

### 4. Regulatory Compliance & Sanctions
- Real-time trade compliance evaluation backed by vector-retrieved legal articles.
- Explicit legal citations (Article, Clause, Legal Document Reference) for every pass/warning decision.
- Authorized supervisor override gate (`compliance:override`) with mandatory justification audit logging.

### 5. Deterministic Route Optimization (VRP)
- High-performance vehicle routing engine (VROOM / OSRM) solving multi-vehicle, multi-stop capacity constraints.
- Rule-based risk engine evaluating cargo weight, volume limits, transit duration, and waypoint count.
- Policy-driven approval gates for high-risk or multi-hub dispatches.

### 6. Real-Time Telematics & Geofencing
- High-frequency GPS telemetry streaming with spatial interpolation.
- Polygon geofence monitoring with automated `GEOFENCE_ENTER` and `GEOFENCE_EXIT` milestone triggers.
- Route corridor deviation detection and dynamic predictive ETA calculation.

### 7. Financial Rating & Settlement
- Multi-currency freight rate matrix calculation with tariff rules.
- Automated customs duty and tax calculation.
- Milestone-triggered invoicing and carrier settlement escrow management.

---

## 5. Governed AI & Human-in-the-Loop Architecture

Aurora rejects uncontrolled autonomous AI. AI operates as a specialized cognitive layer strictly bounded by deterministic rules, mathematical solvers, capability permissions, and mandatory human approval:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                             DETERMINISTIC CORE                              │
│  - VROOM / OSRM Vehicle Routing Engine (Mathematical Solver)                │
│  - Hard Business Rule Engine (Cargo limits, Max stops, Tariff rates)        │
│  - Capability-Based Access Control (CBAC Permissions & Resource Scope)      │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Governed Boundaries
┌─────────────────────────────────────────────────────────────────────────────┐
│                            AI INTELLIGENCE LAYER                            │
│  - Multimodal Document OCR (LayoutLM / Vision Models)                       │
│  - Trade Law RAG Retrieval (Grounded legal citations, Zero-hallucination)   │
│  - Inbound Security Pipeline (AI Phishing & BEC Detection)                  │
│  - Rate Negotiation Drafting (Assisted counter-offers with human approval)  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Mandatory Exception Gate
┌─────────────────────────────────────────────────────────────────────────────┐
│                            HUMAN-IN-THE-LOOP (HITL)                         │
│  - Supervisor Route Approvals (`route_planning:approve`)                    │
│  - OCR Extraction Verification (`ocr:review`)                               │
│  - Compliance Block Overrides (`compliance:override`)                       │
│  - Quarantine Threat Release (`mail:quarantine:release`)                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Security, Isolation & Enterprise Trust

- **Multi-Tenant Data Isolation**: Database-level isolation (`TenantId` global query filters) ensures strict data boundaries between customer organizations.
- **Capability-Based Access Control (CBAC)**: User authority is governed by 37+ granular capability tokens (e.g. `route_planning:approve`, `billing_settlement:settlement:manage`), not ambiguous blanket roles.
- **Identity & Authentication**: Enterprise OIDC integration via AWS Cognito / Azure AD with HttpOnly session cookies.
- **Data Protection & Storage**: S3 / Cloudflare R2 encrypted object storage for raw MIME emails, shipping documents, and PDF invoices.
- **Immutable Audit Logging**: Every administrative action, permission mutation, and compliance override is cryptographically logged with actor attribution.

---

## 7. Public Website Information Architecture & Final Copy

### 01. Top Navigation Bar
- **Logo:** `AURORA` (Inter SemiBold, Cyan Accent Dot)
- **Menu Items:**
  - **Platform** (Shipment Execution, Shared Mail, Route Optimization, Document OCR, GPS Telematics)
  - **Solutions** (Freight Forwarders, Customs Brokers, 3PL & Fleet, Manufacturers)
  - **Governed AI** (Deterministic Core, RAG Compliance, Human-in-the-Loop)
  - **Security & Trust** (Tenant Isolation, CBAC Permissions, Audit Trails)
  - **About**
- **Actions:**
  - `Sign In` (Text link → `/login`)
  - `Book a Demo` (Primary Blue Button → `#contact`)

---

### 02. Hero Section

#### Candidate Headlines:
- **Option 1 (Recommended):**  
  `The Unified Execution Platform for Global Freight & Logistics`
- **Option 2 (Operational Focus):**  
  `Connect Freight Operations, Secure Communication, and Governed AI in One System`
- **Option 3 (Executive Focus):**  
  `End-to-End Supply Chain Orchestration Built for Enterprise Scale`

#### Subheadline:
`Aurora replaces fragmented emails, disconnected spreadsheets, and manual document entry with a unified, deterministic execution engine. Orchestrate shipments, optimize routes, verify customs compliance, and govern AI automation with absolute human accountability.`

#### Primary CTAs:
- `[ Book an Enterprise Demo ]` (Primary Cyan/Blue Glow Button)
- `[ Explore Platform Architecture ]` (Secondary Ghost Button with Arrow)

#### Trust Bar (Hero Bottom):
`ENGINEERED FOR MODERN SUPPLY CHAINS ACROSS PORTS, TERMINALS & GLOBAL HUBS`

---

### 03. Target Industry Trust Strip
Instead of fabricated customer logos, present enterprise logistics sectors built for Aurora:
- `Container Shipping & Ocean Freight`
- `Air Cargo Logistics & Forwarding`
- `Intermodal Rail & Road Transport`
- `Licensed Customs Brokerages`
- `Global 3PL & Distribution Networks`
- `Industrial Manufacturing Supply Chains`

---

### 04. The Operational Reality: Fragmented Logistics vs. Aurora

| Traditional Fragmented Logistics | The Aurora Unified Execution Platform |
| :--- | :--- |
| **Siloed Personal Inboxes**: Inquiries lost in personal staff email accounts; duplicate replies to customers. | **Shared Department Mailboxes**: Single-assignee thread ownership (`PrimaryAssigneeUserId`) with full human auditability. |
| **Manual Document Re-entry**: Hours spent keying Bills of Lading and invoices into outdated TMS software. | **Multimodal Document Intelligence**: Instant structured extraction from PDFs with automated confidence validation. |
| **Compliance Blind Spots**: High customs penalty risks due to manual trade law and tariff verifications. | **Grounded Regulatory RAG**: Automated compliance validation with explicit legal article citations and supervisor override gates. |
| **Disconnected Route Planning**: Route dispatching handled across consumer maps without capacity constraints. | **Deterministic VROOM VRP**: Mathematical multi-stop optimization factoring vehicle payload, volume, and road risk rules. |
| **Black-Box AI Risks**: Uncontrolled chatbots hallucinating rates or making autonomous business mistakes. | **Governed AI Architecture**: AI operates strictly inside deterministic business rules with mandatory human approval gates. |

---

### 05. Platform Capabilities (8 Core Pillars)

#### 1. Shipment Execution & Milestone Tracking
`Full lifecycle visibility from booking confirmation to POD receipt. Track multi-modal milestones across road, ocean, and air freight.`

#### 2. Shared Company Communication
`Eliminate uncoordinated personal inboxes. Departmental shared mailboxes (ops@, customs@) with atomic thread claiming and security filtering.`

#### 3. Multimodal Document OCR
`Extract line items, container numbers, and consignee data from Bills of Lading, Invoices, and Packing Lists in seconds.`

#### 4. Grounded Trade Compliance
`Automate customs regulatory checks with direct citations to published legal codes. Guarantee zero-hallucination compliance audits.`

#### 5. Deterministic Route Optimization
`Solve complex Vehicle Routing Problems (VRP) using mathematical solvers. Minimize mileage while enforcing vehicle weight and volume limits.`

#### 6. Live Telematics & Polygon Geofences
`Real-time GPS tracking with corridor deviation alerts and automated geofence entry/exit milestone triggers.`

#### 7. Freight Rating & Carrier Settlement
`Dynamic freight rating engines, automated customs duty calculations, and milestone-backed escrow payout settlement.`

#### 8. Enterprise Role & Capability Governance
`Protect operational integrity with 37+ granular capability tokens, strict tenant isolation, and immutable audit logging.`

---

### 06. Deep Dive: Governed AI vs. Autonomous Chaos

#### Headline:
`Artificial Intelligence with Enterprise Accountability`

#### Copy:
`Aurora embeds AI as an intelligent assistant inside strict mathematical and legal boundaries. We combine the speed of machine learning with the reliability of deterministic solvers and the authority of human operators.`

#### 3 Pillars of Governed AI:
1. **Mathematical Solvers Over AI Guesswork**: Route planning is computed by VROOM / OSRM optimization engines, guaranteeing optimal payload capacity and transit constraints.
2. **Grounded RAG with Legal Citations**: Regulatory checks cite verified legal articles and national customs directives, completely preventing LLM hallucinations.
3. **Mandatory Human-in-the-Loop (HITL)**: High-risk route plans, sensitive rate counter-offers, and compliance overrides require explicit authorized staff sign-off.

---

### 07. Industry Solutions

#### 1. International Freight Forwarders
`Accelerate quote-to-dispatch cycles. Ingest customer RFQs via shared email, auto-extract packing lists, generate optimized carrier routes, and track containers end-to-end.`

#### 2. Customs Brokers & Compliance Teams
`Eliminate customs clearance delays. Automatically cross-reference commercial invoices against regional trade laws and generate grounded compliance certificates.`

#### 3. 3PL & Fleet Operations
`Maximize fleet utilization and driver efficiency. Run multi-stop VRP optimization, monitor live vehicle telemetry, and receive real-time geofence alerts.`

#### 4. Global Manufacturers & Shippers
`Gain complete supply chain transparency. Track raw material shipments from overseas ports to factory distribution centers with milestone verification.`

---

### 08. Illustrative Operational Transformation Scenarios

> *Note: Clearly labeled as representative operational scenarios.*

#### Scenario A: Cross-Border Multimodal Forwarding
- **The Challenge**: A regional forwarder struggled with 45-minute booking intake delays across uncoordinated staff inboxes and manual B/L transcription errors.
- **The Aurora Workflow**: Inbound customer RFQ automatically routed to `ops@` shared mailbox; Document OCR extracted 14 container line items in 8 seconds; VROOM solver generated a compliant 6-stop feeder route; dispatched with live GPS corridor tracking.
- **Outcome**: *Reduced booking-to-dispatch turnaround from hours to minutes with zero duplicate customer responses.*

#### Scenario B: High-Volume Customs Document Clearance
- **The Challenge**: Clearing 200+ daily import declarations with strict hazardous material checks and fluctuating tariff codes.
- **The Aurora Workflow**: Invoices processed through DocumentOcr; RegulatoryCompliance validated chemical HS codes against national environmental laws; low-confidence items flagged to senior compliance officers via `ocr:review`.
- **Outcome**: *100% auditable compliance records with zero clearance penalties.*

---

### 09. High-Level Enterprise Architecture

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                             CLIENT EXPERIENCES                              │
│  - Aurora Web Operations Portal (React / Next.js Desktop Workspaces)        │
│  - Mobile Telematics & Tracking View                                        │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ HTTPS / WSS
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          INGRESS & API GATEWAY                              │
│  - YARP Reverse Proxy (:443)  •  AWS Cognito OIDC  •  Redis Distributed     │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ High-Speed gRPC
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ENTERPRISE MICROSERVICES PLATFORM                        │
│  - .NET 10: ShipmentWorkflow, RoutePlanning, MailService, DocumentOcr       │
│  - Java 21: ai-governance, devops-agent                                     │
│  - NestJS: billing-service, financial-service, negotiation-agent            │
│  - Event Streaming: RabbitMQ Transactional Outbox                           │
│  - Storage & Data: PostgreSQL Multi-Tenant DBs • Cloudflare R2 Objects      │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 10. Frequently Asked Questions (Buyer FAQ)

#### Q1: What operational scale and logistics modes does Aurora support?
`Aurora is built for inter-facility and international freight execution—including ocean container freight (FCL/LCL), air cargo, intermodal rail, and long-haul road transport between ports, container yards, warehouses, and factories. It is not designed for food or consumer parcel delivery.`

#### Q2: How does Aurora ensure AI does not make costly operational mistakes?
`Aurora enforces a strict Governed AI model. AI assists with document extraction, language classification, and recommendation drafting, but all critical operational decisions—such as route planning, financial rating, and compliance clearance—are computed by deterministic mathematical engines and rule validators, with mandatory human approval gates for high-risk actions.`

#### Q3: Does Aurora support custom email domains and existing mailboxes?
`Yes. Aurora's Mail Platform provisions dedicated tenant domains with DKIM/SPF/DMARC authentication, connecting your shared company mailboxes (e.g. ops@yourcompany.com) directly into operational thread triage.`

#### Q4: How is tenant data isolated and protected?
`Every tenant's operational data is strictly segregated using database-level tenant filters. Authentication is secured via enterprise OIDC (Cognito / Azure AD), and all actions are gated by fine-grained capability permissions with immutable audit logs.`

#### Q5: Can staff approval be required for specific risk thresholds?
`Yes. Administrators can configure custom risk thresholds for cargo weight, volume, stop count, and hazardous material classifications. Any route or shipment exceeding these thresholds automatically triggers a supervisory approval requirement.`

---

### 11. About Aurora & Mission

#### Mission Statement:
`To eliminate operational fragmentation in global logistics by providing a unified, auditable execution platform where deterministic mathematical rigor and governed AI empower logistics professionals to move goods with speed, precision, and confidence.`

---

### 12. Contact & Enterprise Demo Request

#### Header:
`Transform Your Freight Operations`

#### Subtitle:
`Speak with an enterprise logistics architect to see how Aurora can streamline your operations from intake to carrier settlement.`

#### Form Fields:
1. **Full Name** (Text, Required)
2. **Business Email** (Email, Required, e.g. `alex@forwarding-corp.com`)
3. **Company Name** (Text, Required)
4. **Job Role** (Dropdown: `COO / VP Operations`, `Logistics Director`, `Customs Lead`, `IT / Technology Director`, `Other`)
5. **Primary Logistics Challenge** (Dropdown: `Siloed Communications`, `Manual Document Processing`, `Route & VRP Optimization`, `Customs Compliance`, `Full Platform Migration`)
6. **Estimated Monthly Shipments** (Dropdown: `< 500`, `500 - 2,500`, `2,500 - 10,000`, `10,000+`)
7. **Message / Notes** (Text Area, Optional)

#### Primary Action:
`[ Request Enterprise Architecture Demo ]`

---

### 13. Enterprise Footer
- **Column 1 (Platform):** Shipment Execution, Shared Mail, Document OCR, Route Optimization, GPS Telematics, Financial Settlement.
- **Column 2 (Solutions):** Freight Forwarders, Customs Brokers, 3PL & Fleet, Industrial Manufacturers.
- **Column 3 (Trust & Architecture):** Governed AI, Security Architecture, Capability Permissions, Tenant Isolation.
- **Column 4 (Company):** About Aurora, Documentation Index, Contact Sales.
- **Bottom Bar:** `© 2026 Aurora Logistics Platform. Enterprise Multi-Tenant Freight Execution. All rights reserved.` | `Privacy Policy` | `Terms of Service` | `Security Posture`
