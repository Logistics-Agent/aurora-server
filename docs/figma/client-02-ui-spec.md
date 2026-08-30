# Aurora Logistics Platform — Public Website UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/client-01-product-content.md`  
> **Source of Truth:** Audited against `.NET 10` Microservices, `Staff.Bff`, `Admin.Bff`, `ai-governance`, `NestJS` services, and `PermissionConstants.cs`.

---

## 1. Design Direction & Visual Aesthetics

- **Aesthetic:** Enterprise B2B, dark futuristic hero with high-clarity light content surfaces, technical elegance, restrained typography, and functional operational visualizations.
- **Inspirational Quality:** Stripe, Vercel Enterprise, Cloudflare, Linear, Datadog.
- **Color Palette Philosophy:**
  - **Hero & Command Views:** Deep Space Navy (`#0A0F1D`) with subtle geometric coordinate grid lines (`rgba(255,255,255,0.04)`).
  - **Content & Execution Surfaces:** Clean Enterprise White (`#FFFFFF`) and Slate Canvas (`#F8FAFC`).
  - **Primary Accents:** Electric Cyan (`#06B6D4`), Royal Blue (`#2563EB`), Deep Indigo (`#4F46E5`), and Governance Purple (`#8B5CF6`).
  - **Operational Statuses:** Emerald (`#10B981` / Active), Amber (`#F59E0B` / Review Gate), Rose (`#EF4444` / Blocked / Risk), Slate (`#64748B` / Draft).

---

## 2. Design Tokens & 8-Point Spatial Grid

### Spatial Grid System
- Base Unit: `8px` (`8px`, `16px`, `24px`, `32px`, `48px`, `64px`, `96px`, `128px`).
- Section Padding (Desktop): `96px 0` or `128px 0`.
- Section Padding (Mobile): `48px 0` or `64px 0`.
- Container Max-Width: `1240px` (with `24px` gutter padding).

### Typography Hierarchy (`Inter`, `-apple-system`, `sans-serif`)
| Style Name | Size / Line Height | Weight | Usage |
| :--- | :--- | :--- | :--- |
| **Display H1** | `56px / 64px` | Bold (`700`) | Hero Headline (Desktop) |
| **Section H2** | `36px / 44px` | SemiBold (`600`)| Section Titles |
| **Subheading H3**| `22px / 30px` | SemiBold (`600`)| Feature Titles, Card Headers |
| **Body Large** | `18px / 28px` | Regular (`400`) | Hero Subtitle, Lead Paragraphs |
| **Body Regular** | `15px / 24px` | Regular (`400`) | Card descriptions, FAQ answers |
| **Body Small** | `13px / 18px` | Medium (`500`) | Table text, metadata pills, captions |
| **Code / Mono** | `13px / 18px` | Regular (`400`) | Technical codes, HS codes, IDs |

### Elevation & Shadow Tokens
- `elevation-card`: `0 1px 3px 0 rgba(0, 0, 0, 0.05), 0 1px 2px 0 rgba(0, 0, 0, 0.03)`
- `elevation-hover`: `0 10px 25px -5px rgba(15, 23, 42, 0.08), 0 8px 10px -6px rgba(15, 23, 42, 0.04)`
- `elevation-glow-cyan`: `0 0 35px -5px rgba(6, 182, 212, 0.25)`
- `elevation-glow-blue`: `0 0 45px -10px rgba(37, 99, 235, 0.35)`

---

## 3. Global Responsive Viewports

1. **Desktop Large (Primary):** `1440px` canvas (Container `1240px`, 12 columns, `32px` gutter).
2. **Desktop Standard:** `1280px` canvas (Container `1140px`, 12 columns, `24px` gutter).
3. **Tablet Portrait & Landscape:** `768px – 1024px` (8 columns, `20px` gutter).
4. **Mobile (Handheld):** `375px – 430px` (4 columns, `16px` gutter, edge padding `16px`).

---

## 4. Reusable Component Specifications

### 4.1 Navigation Bar (`LandingNav`)
- **Structure:** Floating glassmorphic header (`rgba(10, 15, 29, 0.85)` backdrop-blur: `16px`, border-bottom: `1px solid rgba(255,255,255,0.08)`).
- **Height:** `72px`.
- **Elements:**
  - Left: Brand Logo (`AURORA` in bold uppercase with cyan neon dot `•`).
  - Center: Nav links (`Platform`, `Solutions`, `Governed AI`, `Security`, `About`).
  - Right: `[ Sign In ]` ghost button + `[ Book a Demo ]` high-contrast action button.

### 4.2 Buttons (`Button`)
- **Primary Action (Cyan/Blue Glow):**
  - Background: Linear gradient (`135deg, #06B6D4 0%, #2563EB 100%`).
  - Text: `#FFFFFF` SemiBold `15px`.
  - Radius: `8px`. Padding: `12px 24px`.
  - Hover: Elevation glow + translateY(`-1px`).
- **Secondary Ghost Button:**
  - Background: `rgba(255, 255, 255, 0.06)`, Border: `1px solid rgba(255, 255, 255, 0.15)`.
  - Text: `#F8FAFC` Medium `15px`.
- **Light Surface Primary:**
  - Background: `#0F172A` (Slate 900), Text: `#FFFFFF`.

### 4.3 Badges & Status Pills (`Badge`)
- **Category Badge:** Slate pill (`bg: rgba(255,255,255,0.08), text: #38BDF8, border: 1px solid rgba(56,189,248,0.2)`).
- **Governance Gate Pill:** Amber pill (`bg: #FEF3C7, text: #92400E, icon: ⚡ Human-in-the-Loop`).
- **Deterministic Pill:** Blue pill (`bg: #EFF6FF, text: #1E40AF, icon: ⚙️ VROOM Solver`).

---

## 5. Section-by-Section Wireframe & UI Specifications

### Section 01: Top Navigation Bar
- **Desktop (1440px):** Fixed top bar with logo, horizontal menu items with popover mega-menus for `Platform` and `Solutions`, and dual action buttons.
- **Mobile (375px):** Compressed `56px` bar with logo and hamburger toggle opening a full-screen drawer.

---

### Section 02: Hero Section (Dark Canvas `#0A0F1D`)
- **Layout (Desktop 1440px):** 2-Column Split (Left: Copy & CTAs, 540px; Right: Interactive Execution Workflow Mockup, 640px).
- **Left Column:**
  - Top Badge: `[ ⚡ ENTERPRISE LOGISTICS EXECUTION PLATFORM ]`
  - H1 Headline: `The Unified Execution Platform for Global Freight & Logistics`
  - Subtitle: `Aurora replaces fragmented emails, disconnected spreadsheets, and manual document entry with a unified, deterministic execution engine...`
  - CTA Button Group: `[ Book an Enterprise Demo ]` + `[ Explore Platform Architecture → ]`
  - Micro-trust note: `Built for high-throughput container terminals, ports, and intermodal freight hubs.`
- **Right Column (Interactive Operational Hero Visualizer):**
  - Dark glass card (`rgba(15, 23, 42, 0.75)`, border `1px solid rgba(255,255,255,0.12)`, shadow: `elevation-glow-blue`).
  - Active Step Progression Bar: `1. Intake (Email)` → `2. OCR (B/L)` → `3. Compliance (Sanctions)` → `4. Optimization (VROOM)` → `5. Dispatched (GPS)`.
  - Live preview container displaying a realistic Freight Shipment Card (`SHP-2026-0842 • 2x40HC Ho Chi Minh to Rotterdam`), showing OCR confidence scores (`99.4% B/L extracted`), mathematical routing distance (`14.2 km saved`), and green GPS status pulse.

---

### Section 03: Industry Sectors Trust Strip (Dark Canvas `#0A0F1D`)
- **Layout:** Horizontal 6-item grid displaying industry segments in clean monochrome typography with subtle icons:
  - `[🚢 Container Shipping]` `[✈️ Air Cargo]` `[🚆 Intermodal Rail]` `[📑 Customs Brokerages]` `[🏭 3PL & Fleet]` `[🏢 Global Manufacturers]`

---

### Section 04: The Fragmented Logistics Reality (Light Canvas `#F8FAFC`)
- **Header:** `Why Global Freight Execution Breaks Down`
- **Subtitle:** `Siloed communications and disconnected systems create costly bottlenecks at every handoff.`
- **Layout:** 2-Column Comparison Matrix (Left: "Traditional Fragmented Operations" with Red indicators; Right: "The Aurora Unified Platform" with Green/Blue indicators).
- **5 Comparative Rows:**
  1. Siloed Personal Inboxes vs. Shared Company Mailboxes (`ops@`)
  2. Manual Document Re-entry vs. Multimodal Document OCR
  3. Compliance Blind Spots vs. Grounded Regulatory RAG with Legal Citations
  4. Disconnected Route Planning vs. Deterministic VROOM VRP Solver
  5. Black-Box AI Hallucinations vs. Governed AI with Human Approval Gates

---

### Section 05: The Unified Aurora Freight Workflow (Light Canvas `#FFFFFF`)
- **Header:** `From Inbound Request to Carrier Settlement`
- **Subtitle:** `An unbroken operational pipeline connecting every milestone of the freight lifecycle.`
- **Visual Design:** An interactive horizontal 8-step stepper diagram with pulsing active nodes:
  ```text
  [ 1. Inbound RFQ ] ──► [ 2. Shared Thread ] ──► [ 3. Document OCR ] ──► [ 4. Customs Check ]
           │                                                                     │
  [ 8. Escrow Settlement ] ◄── [ 7. Live GPS Telematics ] ◄── [ 6. Supervisor Gate ] ◄── [ 5. VRP Optimization ]
  ```
- **Step Card Expansion:** Clicking any step reveals detailed input/output data (e.g. clicking `Document OCR` shows raw PDF → extracted JSON with confidence scores).

---

### Section 06: Core Platform Capabilities Grid (Light Canvas `#F8FAFC`)
- **Layout:** 8-Card Responsive Grid (Desktop: 4 Columns × 2 Rows; Mobile: 1 Column Vertical Stack).
- **Card Specifications:** White surface (`#FFFFFF`), border `1px solid #E2E8F0`, hover elevation transition.
- **Card Content:**
  1. **Shipment Lifecycle Management:** Multi-modal state machine, milestone timestamps, hazardous cargo tags.
  2. **Intelligent Business Mail:** Shared company mailboxes (`ops@`, `customs@`), thread ownership, SPF/DKIM screening.
  3. **Multimodal Document OCR:** Bills of Lading, Invoices, Packing Lists extraction with human review queues.
  4. **Grounded Trade Compliance:** Vector legal retrieval citing national customs articles with override gates.
  5. **Deterministic Route Optimization:** VROOM / OSRM mathematical solver with vehicle capacity constraints.
  6. **Live Telematics & Geofences:** High-frequency GPS streaming, polygon geofence alerts, dynamic ETA.
  7. **Financial Rating & Settlement:** Multi-currency freight tariffs, duty calculation, milestone escrow release.
  8. **Enterprise Access & Governance:** 37+ granular capability tokens, strict tenant isolation, cryptographic audit.

---

### Section 07: Governed AI vs. Autonomous Chaos (Dark Canvas `#0A0F1D`)
- **Header:** `Governed AI Embedded Inside Deterministic Workflows`
- **Subtitle:** `Zero hallucinations. Mathematical solvers for logistics. Human authority for business decisions.`
- **Visual Diagram (3-Tier Layered Architecture):**
  - **Tier 1 (Deterministic Base):** VROOM VRP Solver, Business Rule Engine, Tariff Rate Matrix.
  - **Tier 2 (Governed AI Layer):** LayoutLM Document OCR, Customs RAG Retrieval, Inbound Threat Classifier.
  - **Tier 3 (Human Authority):** Route Approval (`route_planning:approve`), OCR Review (`ocr:review`), Compliance Override (`compliance:override`).

---

### Section 08: Shared Mail & Communication Architecture (Light Canvas `#FFFFFF`)
- **Header:** `Centralize Customer Communication with Complete Accountability`
- **Layout:** 2-Column Split (Left: Architectural explanation; Right: High-fidelity mockup of the Aurora Shared Thread Queue).
- **Key Callouts:**
  - Single Assigned Owner (`PrimaryAssigneeUserId` prevents duplicate replies).
  - Multi-stage Inbound Security Pipeline (SPF, DKIM, ClamAV malware scan, AI phishing classifier).
  - Outbound Human Attribution (`SentByUserId` logged on every customer email).

---

### Section 09: Document Intelligence to Compliant Routing (Light Canvas `#F8FAFC`)
- **Header:** `Connected Intelligence: OCR → Compliance → Optimization`
- **Visual Workflow Card:** Shows a live Bill of Lading PDF being uploaded on the left, automatically populating structured cargo line items in the center, and feeding directly into a VROOM-optimized multi-stop delivery route on the right.

---

### Section 10: Real-Time Fleet Visibility & Telematics (Dark Canvas `#0A0F1D`)
- **Header:** `Live Telematics, Geofences & Exception Management`
- **Visual:** Full-width dark telemetry map mockup displaying:
  - Vector road corridors with GPS breadcrumb trail.
  - Interactive polygon geofences (`Port Terminal 4`, `Bonded Warehouse B`).
  - Real-time exception event log: `[09:14] Geofence Entry Confirmed`, `[09:22] ETA recalculated: On Time`.

---

### Section 11: Enterprise Security, Governance & Compliance (Light Canvas `#FFFFFF`)
- **Header:** `Built for Enterprise Security & Strict Tenant Isolation`
- **Layout:** 4-Column Security Grid:
  1. **Tenant Isolation:** Database-level tenant partitioning and fail-closed query filters.
  2. **Capability Access Control:** 37+ fine-grained permission tokens over ambiguous broad roles.
  3. **Identity & OIDC:** Enterprise SSO via AWS Cognito and Azure AD with HttpOnly cookies.
  4. **Cryptographic Audit:** Immutable audit trail logging actor, action, and target resource ID.

---

### Section 12: Business Outcomes & Enterprise Scenarios (Light Canvas `#F8FAFC`)
- **Header:** `Measurable Operational Transformation`
- **Banner Note:** `Illustrative Enterprise Operational Scenarios`
- **3 Outcome Cards:**
  - **Scenario 1:** Cross-Border Multimodal Forwarding (Intake turnaround reduced from 45 min to < 3 min).
  - **Scenario 2:** High-Volume Customs Clearance (200+ daily declarations cleared with zero tariff penalties).
  - **Scenario 3:** Regional Fleet Logistics (18% reduction in empty vehicle miles via VROOM multi-stop optimization).

---

### Section 13: Solutions by Industry (Light Canvas `#FFFFFF`)
- **Tabbed Interface:**
  - `[ Freight Forwarders ]` `[ Customs Brokers ]` `[ 3PL & Fleet ]` `[ Manufacturers ]`
- **Tab Content:** Tailored problem statement, specific Aurora modules used, and operational workflow blueprint.

---

### Section 14: High-Level System Architecture (Dark Canvas `#0A0F1D`)
- **Header:** `Modern Cloud-Native Microservices Architecture`
- **Layout:** High-level interactive architectural block diagram:
  - Client Layer (React / Next.js) → YARP API Gateway → .NET 10 Microservices + Java AI Governance + NestJS Billing → PostgreSQL Multi-Tenant DBs & Cloudflare R2.

---

### Section 15: Buyer FAQ Accordion (Light Canvas `#FFFFFF`)
- **Layout:** 5 Expandable clean accordion cards addressing key enterprise buyer concerns (Operational scale, AI safety, custom email domains, tenant data isolation, risk approval thresholds).

---

### Section 16: About Aurora & Mission (Light Canvas `#F8FAFC`)
- **Layout:** Centered mission statement card with core architectural values (Mathematical Rigor, Governed AI, Operational Transparency, Human Accountability).

---

### Section 17: Enterprise Demo Request Form (Dark Canvas `#0A0F1D`)
- **Layout:** 2-Column Split (Left: Value reinforcement & architect contact note; Right: Clean 7-field enterprise form card with glassmorphism).
- **Form Card:** Dark glass (`rgba(15, 23, 42, 0.8)`), inputs with clean slate borders (`#334155`), focus ring (`#06B6D4`), and high-contrast submit button: `[ Request Enterprise Architecture Demo ]`.

---

### Section 18: Enterprise Footer (Dark Canvas `#070B14`)
- **Layout:** 4-Column navigation links + bottom copyright and legal bar (`Privacy Policy`, `Terms of Service`, `Security Posture`).

---

## 6. Micro-Interactions & Motion Design

1. **Workflow Progression Animation:** Subtle neon cyan pulse traveling across connector lines between workflow nodes (duration: `3s`, linear loop).
2. **Card Hover States:** Gentle elevation lift (`translateY(-3px)`), border highlight shift from `#E2E8F0` to `#38BDF8`, soft shadow transition (`200ms ease-out`).
3. **Accordion Transition:** Smooth height expansion (`300ms cubic-bezier(0.16, 1, 0.3, 1)`).
4. **Reduced Motion Rule:** All animations wrap with `@media (prefers-reduced-motion: reduce)` to disable non-essential motion.

---

## 7. Responsive Redesign Rules

| Screen Component | Desktop (1440px / 1280px) | Tablet (768px – 1024px) | Mobile (375px – 430px) |
| :--- | :--- | :--- | :--- |
| **Hero Section** | 2-Column Split (Copy + Interactive Mockup) | 2-Column Compressed | 1-Column Stacked (Copy top, simplified visual below) |
| **Workflow Diagram** | 8-Step Horizontal Interactive Stepper | 4×2 Grid Matrix | Vertical Connected Stepper with expanders |
| **Capabilities** | 4-Column × 2-Row Grid | 2-Column × 4-Row Grid | 1-Column Stack with swipeable pagination |
| **Comparison Matrix**| Side-by-Side Dual Column Table | Stacked 2-Column Cards | Segmented Toggle (`Traditional` vs `Aurora`) |
| **Contact Form** | 2-Column (Text left, Form right) | 1-Column Stacked | 1-Column Stacked (Full-width form controls) |

---

## 8. Accessibility & Inclusivity Specifications

- **Color Contrast:** All body text meets WCAG AA `4.5:1` minimum against backgrounds (`#0F172A` on `#FFFFFF`, `#F8FAFC` on `#0A0F1D`).
- **Focus Indicators:** Explicit `2px solid #06B6D4` outline with `2px` offset on all interactive buttons, inputs, and links.
- **Form Accessibility:** All form fields have persistent `<label>` elements and `aria-required="true"` attributes.
- **Semantic Structure:** Single `<h1>` in Hero, sequential `<h2>` section headers, and semantic `<nav>`, `<main>`, `<section>`, `<footer>` landmarks.

---

## 9. Figma Frame Checklist (15 Artboards)

Construct the following 15 distinct frames in Figma:

- [ ] `01_Landing_Desktop_1440` — Complete full-page landing design.
- [ ] `02_Landing_Desktop_1280` — Standard desktop layout variation.
- [ ] `03_Landing_Tablet_768` — Tablet portrait responsive adaptation.
- [ ] `04_Landing_Mobile_375` — Full mobile vertical layout.
- [ ] `05_Nav_Expanded_MegaMenu` — Platform and Solutions mega-menu dropdown states.
- [ ] `06_Hero_Visual_Detail` — High-fidelity close-up of the interactive shipment visualizer.
- [ ] `07_Unified_Workflow_Diagram` — 8-step connected freight execution diagram.
- [ ] `08_Governed_AI_Architecture` — 3-tier deterministic vs AI vs human authority diagram.
- [ ] `09_Shared_Mail_Mockup` — High-fidelity shared thread queue and security pipeline UI.
- [ ] `10_Telematics_Map_Mockup` — Real-time GPS corridor and geofence tracking view.
- [ ] `11_System_Architecture_Diagram` — High-level microservices and infrastructure topology.
- [ ] `12_Contact_Form_ActiveState` — Form with populated inputs and validation states.
- [ ] `13_Contact_Form_SuccessModal` — Post-submission confirmation modal.
- [ ] `14_Mobile_Nav_Drawer` — Full-screen mobile navigation overlay.
- [ ] `15_Design_System_Components` — Complete UI kit with tokens, buttons, badges, and card variants.

---

## 10. React / Next.js Component Architecture Mapping

```text
src/components/landing/
├── LandingNav.tsx                   (Floating glassmorphic header)
├── HeroSection.tsx                  (H1, CTAs, interactive shipment mockup)
├── TrustStrip.tsx                   (6 industry segment badges)
├── ProblemComparison.tsx            (Fragmented vs Aurora 5-row matrix)
├── UnifiedWorkflow.tsx              (8-step freight execution pipeline)
├── CapabilitiesGrid.tsx             (8 core platform capability cards)
├── GovernedAiArchitecture.tsx       (3-tier deterministic & AI architecture)
├── SharedMailIntelligence.tsx       (Shared mailboxes & thread triage view)
├── DocumentToRouteFlow.tsx          (OCR → Compliance → VRP routing card)
├── RealtimeTelematics.tsx           (Dark map mockup with geofence triggers)
├── SecurityGovernance.tsx           (4-pillar security & tenant isolation)
├── BusinessOutcomes.tsx             (3 illustrative enterprise scenarios)
├── IndustrySolutions.tsx            (Tabbed solutions for forwarders/customs)
├── SystemArchitecture.tsx           (High-level cloud-native block diagram)
├── BuyerFaqAccordion.tsx            (5 enterprise buyer FAQ cards)
├── AboutMission.tsx                 (Company mission & core values)
├── ContactDemoForm.tsx              (7-field enterprise demo request form)
└── EnterpriseFooter.tsx             (4-column navigation & legal bar)
```
