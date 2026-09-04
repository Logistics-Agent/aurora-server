# Aurora Tenant Admin Operations UI — UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/admin-ops-01-product-context.md`  
> **Source of Truth:** Audited against `.NET 10` `IamTenant`, `Admin.Bff`, `RoutePlanningAgent`, `RegulatoryCompliance`, and `PermissionConstants.cs`.

---

## 1. Design Direction & System Tokens

- **Aesthetic:** Enterprise B2B, serious, high-density, security-conscious, modern SaaS control plane.
- **Reference Standards:** Cloudflare Dashboard, Microsoft Entra Admin Center, AWS IAM, Stripe Dashboard.
- **Grid System:** 8-point spatial grid (`8px`, `16px`, `24px`, `32px`, `48px`).
- **Primary Viewport:** Desktop-first (1440px primary, 1280px supported, 1024px minimum).

### Design Tokens
| Token Category | Value / Spec | Usage |
| :--- | :--- | :--- |
| **Typography** | `Inter`, `-apple-system`, `sans-serif` | Clean, highly legible UI font |
| **H1 (Page Title)** | `24px / 32px (Bold, SemiBold)` | Primary screen headers |
| **H2 (Section)** | `18px / 24px (SemiBold)` | Subsection and drawer headers |
| **Body (Default)** | `14px / 20px (Regular)` | Table cells, form labels, body text |
| **Body (Small/Mono)**| `12px / 16px (Mono / Medium)` | Permission codes, timestamps, IDs |
| **Primary Color** | `#2563EB` (Blue 600) | Brand primary, action buttons, active states |
| **Surface Colors** | `#FFFFFF` (Base), `#F8FAFC` (Canvas), `#F1F5F9` (Subtle) | Light mode background hierarchy |
| **Border Color** | `#E2E8F0` (Slate 200) | Data table dividers, card strokes |
| **Success Status** | `#10B981` (Emerald 500), bg `#ECFDF5` | `ACTIVE`, `SUCCESS`, `GRANTED` |
| **Warning / Elevated**| `#F59E0B` (Amber 500), bg `#FFFBEB` | `INVITED`, `ELEVATED PERMISSION` |
| **Critical / Danger** | `#EF4444` (Red 500), bg `#FEF2F2` | `SUSPENDED`, `REVOKED`, `ADMIN ACTION` |
| **Elevation Shadows** | `sm: 0 1px 2px rgba(0,0,0,0.05)`, `md: 0 4px 6px -1px rgba(0,0,0,0.1)` | Cards, dropdown menus, slide drawers |

---

## 2. Global Admin Layout & Navigation

### Shell Layout Structure (1440px)
```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Header (Height: 56px) | Logo: Aurora Admin | Tenant: Acme Logistics [v] | User Menu │
├──────────────┬──────────────────────────────────────────────────────────────┤
│ Sidebar      │ Main Content Canvas (Padding: 24px 32px)                      │
│ (Width:240px)│                                                              │
│              │ Breadcrumbs: Admin / [Section] / [Active Page]               │
│ [Overview]   │ Page Header (Title, Subtitle, Primary Actions)               │
│              │                                                              │
│ PEOPLE &     │ Filter Toolbar & Search Bar                                  │
│ ACCESS       │                                                              │
│ • Users      │ Primary Content Area (Data Tables, Configuration Cards)       │
│ • Roles      │                                                              │
│ • Permissions│                                                              │
│              │ Pagination Footer & Summary Counts                           │
│ OPERATIONS   │                                                              │
│ CONFIG       │                                                              │
│ • Route Rules│                                                              │
│ • AI Policy  │                                                              │
│ • Knowledge  │                                                              │
│              │                                                              │
│ MAIL (Suite) │                                                              │
│ • Mail Admin ├──► (Opens Admin Mail Module)                                 │
│              │                                                              │
│ AUDIT        │                                                              │
│ • Audit Log  │                                                              │
└──────────────┴──────────────────────────────────────────────────────────────┘
```

---

## 3. Screen 1: Admin Overview (`/admin/overview`)

**Purpose:** Executive summary of tenant organization health, team size, elevated access, and policy versions.

### 3.1 Header
- **Title:** `Tenant Administration Overview`
- **Subtitle:** `Acme Logistics Corp (Tenant Code: ACME) • Enterprise Plan`
- **Actions:** `[+ Invite User]` `[Configure Route Rules]`

### 3.2 Metric Summary Cards (4 Columns)
| Card Title | Value | Subtext / Indicator | Click Action |
| :--- | :--- | :--- | :--- |
| **Total Users** | `24` | `21 Active • 2 Invited • 1 Suspended` | View Users → |
| **Elevated Capabilities**| `7` | `5 Users possess supervisory/approval rights` | Review Access → |
| **Active Route Policy** | `v2.1` | `Published on Aug 20, 2026 • 7 Rules Active` | Manage Rules → |
| **Shared Mailboxes** | `6` | `2 Domains Configured • 3 Pending Quarantine`| Mail Admin → |

### 3.3 Dashboard Widgets (2 Columns)
- **Widget A: Pending User Invitations & Recent Onboarding (5 rows)**:
  - Columns: Name, Email, Base Role, Invited Date, Action (`Resend Invite`, `Cancel`).
- **Widget B: Recent IAM & Policy Audit Activity (5 rows)**:
  - Columns: Timestamp, Admin Actor, Action (`DirectPermissionsUpdated`, `UserRoleChanged`), Target User, Result.

---

## 4. Screen 2: Users & Staff Management (`/admin/users`)

**Purpose:** Team directory, account lifecycle management, and direct capability overview.

### 4.1 Filter Toolbar
- **Search:** Input with icon searching Name, Email, or Staff Code.
- **Role Filter:** Dropdown `All Roles` | `STAFF` | `MANAGER` | `TENANT_ADMIN`.
- **Status Filter:** Dropdown `All Statuses` | `Active` | `Invited` | `Suspended`.
- **Primary Action:** `[+ Invite User]` *(Requires `iam:user:invite`)*

### 4.2 Users Table
| Column Name | Data Field | Component Type | Value Example |
| :--- | :--- | :--- | :--- |
| **Name & Email** | `FirstName` + `LastName`<br/>`Email` | Avatar + Text Bold<br/>Text Subtitle | **Alex Nguyen**<br/>`alex.nguyen@acme.com` |
| **Base Role** | `Role` | Role Badge | `STAFF` (Blue) \| `MANAGER` (Purple) \| `TENANT_ADMIN` (Amber) |
| **Status** | `Status` | Status Badge | `Active` (Green) \| `Invited` (Yellow) \| `Suspended` (Gray) |
| **Direct Capabilities**| `Permissions.Count` | Pill + Tooltip | `12 Capabilities` *(Includes 2 Elevated)* |
| **Version** | `PermissionVersion` | Text Mono | `v.4` |
| **Created** | `CreatedAt` | Timestamp | `2026-08-10` |
| **Actions** | — | Dropdown Menu | `View Details`, `Edit Capabilities`, `Change Role`, `Suspend User`, `Reset Password` |

### 4.3 Bulk Selection Action Bar (Appears when >= 1 checkbox selected)
- **Selection Count:** `3 Users Selected`
- **Actions:**
  - `[Manage Capabilities (Bulk)]` *(Opens Bulk Delta Modal)*
  - `[Change Base Role]`
  - `[Deactivate Selected]` *(Destructive)*

---

## 5. Screen 3: User Detail & Capability Drawer (Width: 640px)

**Purpose:** Comprehensive view of a user's identity, Base Role, and direct capability tokens.

### 5.1 Drawer Header
- **Avatar & Name:** Alex Nguyen (`alex.nguyen@acme.com`)
- **Status Badge:** `ACTIVE` (Green)
- **User ID:** `9a3c7e81-7788-4221-9988-112233445566` (Copy icon)

### 5.2 Tab 1: Access & Direct Capabilities
- **Base Role Section:**
  - Current Role: `STAFF` (Tenant Staff Persona)
  - Action Button: `[Change Base Role]`
- **Active Capabilities Summary:**
  - Progress bar: `12 / 37 System Capabilities Assigned`
  - Elevated badge counter: `2 Elevated Capabilities Active`
- **Grouped Permission Accordion (Domain breakdown):**
  - **Route Planning (4 Granted):**
    - `route_planning:read` (Standard)
    - `route_planning:create` (Standard)
    - `route_planning:optimize` (Standard)
    - `route_planning:approve` (⚡ Elevated — Route Approval Authority)
  - **Mail Platform (4 Granted):**
    - `mail:read`, `mail:send`, `mail:draft:create`, `mail:thread:claim`
  - **OCR & Review (1 Granted):**
    - `ocr:review` (⚡ Elevated — Human Review Queue)
- **Drawer Footer Actions:**
  - `[Edit Direct Capabilities]` *(Opens Permission Matrix Editor)*

### 5.3 Tab 2: Profile & Account Settings
- **First Name / Last Name:** Editable inputs.
- **Phone Number:** `+84 90 123 4567`.
- **Administrative Actions:**
  - `[Send Password Reset Email]`
  - `[Suspend User Account (Red)]`

---

## 6. Screen 4: Invite User Modal (Dialog, Width: 520px)

**Purpose:** Onboard a new employee into the tenant organization.

### 6.1 Modal Form Fields
1. **First Name** & **Last Name** (Text inputs, Required).
2. **Email Address** (Email input, Required, e.g. `jane.doe@acmelogistics.com`).
3. **Phone Number** (Optional international format).
4. **Base Role Selection** (Radio Card Group):
   - `[ (•) STAFF ]` — Standard operational staff persona.
   - `[ ( ) MANAGER ]` — Supervisory oversight persona.
   - `[ ( ) TENANT_ADMIN ]` — Full organization admin persona.
5. **Apply Default Permissions Toggle** (Checked by default):
   - Helper text: `"Automatically grant the baseline capabilities associated with the selected role."`
6. **Additional Initial Capabilities** (Collapsible multi-select for elevated roles e.g. `ocr:review`, `route_planning:approve`).

### 6.2 Modal Actions
- `[Cancel]` | `[Send Invitation & Assign Access]`

---

## 7. Screen 5: Direct Capability Matrix Editor (Modal / Drawer, Width: 720px)

**Purpose:** Granular delta grant/revoke interface for direct user permissions.

### 7.1 Search & Filter Toolbar
- **Search Capabilities:** Text input filtering capability names and codes.
- **Domain Filter:** Dropdown (All Domains, Route Planning, Mail, Invoicing, etc.).
- **View Filter:** `All Capabilities` | `Granted Only` | `Elevated Only`.

### 7.2 Capability Group Accordion (10 Business Domains)
Each capability item renders as an interactive toggle card:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ [✓] Approve High-Risk Routes                       [ ⚡ ELEVATED ] [ GRANTED ]│
│     Code: route_planning:approve                                            │
│     Allows this user to approve route plans that exceed risk thresholds.    │
└─────────────────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────────────────────┐
│ [ ] Override Customs Compliance                    [ ⚡ ELEVATED ] [ REVOKED ]│
│     Code: compliance:override                                               │
│     Allows manual override of regulatory embargoes and compliance blocks.   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 7.3 Delta Change Summary Bar (Sticky at Bottom)
- **Summary:** `Changes to apply: +1 Grant (route_planning:approve), -1 Revoke (compliance:override)`
- **Actions:** `[Reset Changes]` | `[Review & Apply Changes]`

---

## 8. Screen 6: Elevated Permission Confirmation Dialog (Modal, Width: 460px)

**Purpose:** Security guardrail preventing accidental assignment of sensitive or supervisory powers.

### 8.1 Dialog Layout
- **Icon:** Amber Warning Shield (`AlertTriangle`)
- **Title:** `Grant Elevated Authority?`
- **Body:**
  - `"You are granting the following elevated capability to Alex Nguyen:"`
  - **`route_planning:approve` (Approve High-Risk Routes)**
  - `"This user will be permitted to bypass automated route risk warnings and authorize vehicle dispatches without managerial oversight."`
- **Actions:** `[Cancel]` | `[Confirm & Grant Authority (Amber Button)]`

---

## 9. Screen 7: Bulk Permission Update Modal (Dialog, Width: 640px)

**Purpose:** Apply delta capability changes across multiple selected team members safely.

### 9.1 Dialog Layout
- **Banner:** `"Modifying capabilities for 5 selected users. Existing custom capabilities not specified below will be preserved."`
- **Action Mode:** Segmented control: `[ Grant Capabilities ]` | `[ Revoke Capabilities ]`
- **Capability Multi-Select:** Checkboxes for domain capabilities.
- **Example:** Selecting `Grant: ocr:review`.
- **Actions:** `[Cancel]` | `[Apply Delta Updates to 5 Users]`

---

## 10. Screen 8: Roles & Personas Explorer (`/admin/roles`)

**Purpose:** Inspect canonical Base Role definitions and their default onboarding templates.

### 10.1 Role Definition Cards (3 Cards Grid)
1. **`STAFF` (Tenant Staff)**:
   - Description: Standard operational staff persona for day-to-day freight execution.
   - Baseline Template: 10 capabilities (`shipments:create`, `route_planning:optimize`, `mail:send`, etc.).
   - Assigned Users: `18 Users`.
2. **`MANAGER` (Operations Manager)**:
   - Description: Operations supervisor persona for team triage and risk approvals.
   - Baseline Template: 22 capabilities (`route_planning:approve`, `mail:thread:reassign`, `ocr:review`, etc.).
   - Assigned Users: `4 Users`.
3. **`TENANT_ADMIN` (Tenant Administrator)**:
   - Description: Full organization administrator across tenant services and settings.
   - Baseline Template: All tenant-scoped capabilities (`iam:*`, `mail:domain:manage`, etc.).
   - Assigned Users: `2 Users`.

---

## 11. Screen 9: Route Risk Rules Configuration (`/admin/config/route-rules`)

**Purpose:** Manage thresholds for the 7 canonical route planning risk rules.

### 11.1 Rule Configuration Cards
| Rule Code | Rule Name | Configurable Thresholds | Activation Toggle |
| :--- | :--- | :--- | :--- |
| **`HeavyWeightRule`** | Heavy Cargo Limit | `Max Weight (kg): [ 10,000 ]` | `[ (•) Enabled ]` |
| **`LargeVolumeRule`** | High Volume Cargo | `Max Volume (m³): [ 50.0 ]` | `[ (•) Enabled ]` |
| **`RouteStopCountRule`** | Maximum Waypoint Stops | `Max Stop Count: [ 12 ]` | `[ (•) Enabled ]` |
| **`LongDurationRule`** | Long Driver Duration | `Max Duration (Hours): [ 8.0 ]` | `[ (•) Enabled ]` |
| **`MinimumStopsRule`** | Minimum Waypoint Stops | `Min Stop Count: [ 2 ]` | `[ (•) Enabled ]` |
| **`MultiHubRule`** | Multi-Hub Dispatch | `Require Multi-Hub Flag: [ Yes ]` | `[ (•) Enabled ]` |
| **`OnDemandTypeRule`** | On-Demand Urgent Cargo | `Trigger Immediate Risk: [ Yes ]` | `[ (•) Enabled ]` |

### 11.2 Actions
- Each card has `[Save Rule Configuration]` *(Requires `route_planning:policy:manage`)*.

---

## 12. Screen 10: AI Automation Policy (`/admin/config/ai-policy`)

**Purpose:** Control AI autonomy levels and Foundation Model providers for logistics features.

### 12.1 Configuration Card: Route Planning AI Automation
- **Feature Code:** `RoutePlanning`
- **Automation Policy Mode (Dropdown):**
  - `Manual` — AI recommendations disabled; purely human route planning.
  - `RulesOnly` — Deterministic VROOM & rule engine only.
  - `RulesAndLlm` — Rule engine with AI route recommendations and explanation summaries.
  - `RulesLlmApproval` — AI recommendations require manager approval before dispatch.
- **AI Model Provider (Dropdown):**
  - `Google Gemini (1.5 Flash / Pro)`
  - `Azure OpenAI (GPT-4o)`
- **Status Toggle:** `[ (•) Active ]`
- **Action:** `[Update AI Policy]` *(Requires `route_planning:policy:manage`)*

---

## 13. Screen 11: Knowledge & SOP Documents (`/admin/config/knowledge`)

**Purpose:** Ingest and manage custom tenant operational guidelines and SOPs for RAG search.

### 13.1 Knowledge Documents Table
- **Columns:** Document Title, Category (`SOP`, `CarrierContract`, `CustomsGuideline`), Language, Version, Chunk Count, Ingested Date.
- **Primary Action:** `[+ Upload SOP Document]` *(Requires `documents:ingest`)*
- **Upload Modal:** File selector (PDF/DOCX), Title input, Category dropdown.

---

## 14. Shared Components & Token Specifications

1. **Role Badges:**
   - `STAFF`: Blue (`bg: #EFF6FF, text: #1D4ED8, border: #DBEAFE`)
   - `MANAGER`: Purple (`bg: #FAF5FF, text: #6B21A8, border: #F3E8FF`)
   - `TENANT_ADMIN`: Amber (`bg: #FFFBEB, text: #B45309, border: #FEF3C7`)
2. **Capability Risk Tags:**
   - `Standard`: Slate (`bg: #F1F5F9, text: #475569`)
   - `Elevated`: Amber with Lightning icon ⚡ (`bg: #FEF3C7, text: #92400E`)
   - `Admin`: Rose (`bg: #FFE4E6, text: #9F1239`)
3. **Data Table Pagination:**
   - Standard cursor pagination (`Page 1 of 3 • 24 Users Total`, `[Previous] [Next]`).

---

## 15. Permission-Based UI Gating Rules

| User Permission State | UI Component State |
| :--- | :--- |
| **Has `iam:user:invite`** | `+ Invite User` button active. |
| **Missing `iam:user:invite`** | `+ Invite User` button hidden. |
| **Has `iam:permission:manage`** | `Edit Direct Capabilities` and permission checkboxes active. |
| **Missing `iam:permission:manage`**| Permission checkboxes read-only; edit button disabled with tooltip `"Requires iam:permission:manage"`. |
| **Has `route_planning:policy:manage`**| Route rule threshold inputs and AI policy dropdowns enabled. |
| **Missing `route_planning:policy:manage`**| Route rule cards display in read-only view. |

---

## 16. State Variations (Empty, Loading, Error, Forbidden)

1. **Loading State:** Skeleton table rows (5 rows) with animated pulse effect.
2. **Empty Search State:**
   - Icon: `UserX` (Slate 400)
   - Heading: `"No users found"`
   - Subtext: `"No team members matched your search criteria. Try adjusting your filters."`
3. **Forbidden State (403):**
   - Icon: `ShieldAlert` (Rose 500)
   - Heading: `"Administrative Access Restricted"`
   - Subtext: `"You do not possess the required capability permission to view this administrative console."`

---

## 17. Figma Frame Checklist

Generate the following 14 distinct artboard frames for the Tenant Admin Operations Suite:

- [ ] `01_Admin_Overview` — Full executive dashboard with metrics and widgets.
- [ ] `02_Users_Directory` — Paged user table with search, role badges, and status pills.
- [ ] `03_Users_BulkSelected` — User table showing multi-select toolbar (3 users selected).
- [ ] `04_Users_InviteModal` — Invite new team member dialog with role preset radio cards.
- [ ] `05_UserDetail_Drawer_AccessTab` — User drawer showing Base Role & grouped capabilities.
- [ ] `06_UserDetail_Drawer_ProfileTab` — User drawer showing profile edit and password reset.
- [ ] `07_PermissionMatrix_Editor` — Granular capability editor with domain accordions.
- [ ] `08_ElevatedPermission_ConfirmDialog` — Consequence warning modal for elevated access.
- [ ] `09_BulkPermission_UpdateModal` — Delta grant/revoke dialog for multi-user update.
- [ ] `10_RoleCatalog_Explorer` — 3 canonical role cards with default template viewers.
- [ ] `11_RouteRiskRules_Config` — Grid of 7 configurable rule cards with threshold inputs.
- [ ] `12_AiAutomationPolicy_Config` — AI automation mode selector and model provider dropdown.
- [ ] `13_KnowledgeSop_Directory` — Tenant SOP document inventory and upload drawer.
- [ ] `14_User_SuspendConfirmDialog` — Destructive account suspension modal.

---

## 18. Do Not Design (Guardrails)

1. **No `StaffType` Enums**: Do not design legacy department tags (Operations, Finance, CS).
2. **No Platform Super-Admin Screens**: Do not design tenant provisioning, plan pricing, or global database configs in this Tenant Admin UI.
3. **No Staff Operational Queues**: Do not design shipment creation workflows, dispatch maps, or email composing inside this console.
4. **No Fake Telemetry**: Do not design imaginary productivity scores or ROI percentage gauges.
