# Aurora Tenant Admin Operations UI — Product Context

> **Design Target:** Figma AI / UI Designer Reference Specification  
> **Source of Truth:** Audited against `.NET 10` `IamTenant`, `Admin.Bff`, `RoutePlanningAgent`, `RegulatoryCompliance`, `PermissionConstants.cs`, and `protos/iam_tenant.proto`.

---

## 1. Product Summary

**Aurora** is an enterprise multi-tenant B2B SaaS logistics and freight execution platform. The **Tenant Administration UI (Admin Operations Console)** is the primary control plane for the organizational administrator (`TENANT_ADMIN`).

The Admin Console enables the organization to:
1. Manage user identities, team onboarding, and lifecycle states (Invited, Active, Suspended).
2. Assign canonical persona roles (`STAFF`, `MANAGER`, `TENANT_ADMIN`) and govern direct capability-based permissions.
3. Configure tenant-level operational policies, including vehicle routing risk thresholds, automated AI governance policies, and company operational guides (SOPs).
4. Provide a unified administrative entry point across all tenant capabilities, including email infrastructure (Admin Mail).

The Admin Console is strictly separated from daily operational triage (`Staff.Bff` / Staff Operations UI) and global multi-tenant platform administration (`System.Bff` / System Admin UI).

---

## 2. Primary User

**Persona:** `TENANT_ADMIN` / Organization IT Administrator & Operations Director

### Primary Goals:
- Onboard new staff members and grant precise operational permissions.
- Audit and adjust direct capabilities for operational specialists (e.g. assigning route approval authority to a senior operator).
- Deactivate departing staff instantly across all tenant services.
- Tune logistical risk rules (e.g. maximum stop count, heavy cargo limits) without developer intervention.
- Configure AI automation levels (Manual, Rules-only, or Rules + LLM) for company workflows.

---

## 3. Admin Responsibilities & Boundaries

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                             SYSTEM_ADMIN                                    │
│  - System.Bff: Tenant onboarding, plan tiers, global law ingestion          │
│  - Stalwart Server UI: Server clustering, SMTP listeners, global TLS        │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Tenant Boundary
┌─────────────────────────────────────────────────────────────────────────────┐
│                 TENANT_ADMIN (Admin Operations Console)                     │
│  - People & Access: User onboarding, Base Roles, Direct Capability Grants   │
│  - Operations Config: Route risk rules, AI automation policies, Tenant SOPs │
│  - Mail Admin: Company mailboxes, custom domains, aliases, quarantine       │
│  - Tenant Audit: IAM mutations and security audit logs                      │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Operational Queue
┌─────────────────────────────────────────────────────────────────────────────┐
│                      STAFF / MANAGER (Staff Work UI)                        │
│  - Day-to-day shipment processing, route creation, and dispatch             │
│  - Shared email triage, thread claiming, drafting, and sending              │
│  - Real-time GPS fleet monitoring and alert resolution                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

| Domain Area | Tenant Admin Console (`Admin.Bff`) | Staff Work UI (`Staff.Bff`) | System Admin (`System.Bff`) |
| :--- | :--- | :--- | :--- |
| **User Identity** | Invite user, update profile, suspend/activate | View own profile & preferences | Create tenant admin, manage tenant status |
| **Permissions** | Grant/revoke direct capabilities, change role | None (read own capabilities) | System-only permission management |
| **Route Policies** | Configure rule thresholds, AI provider & policy | Execute routes, request approval | None |
| **Knowledge / SOP**| Ingest tenant-specific SOPs and guidelines | Search & query RAG knowledge | Ingest national laws (PLATFORM scope) |
| **Mail Management**| Provision domains, create shared mailboxes | Claim threads, compose drafts | Requeue dead-letter pipeline |

---

## 4. Current IAM & Authorization Model

Aurora implements a **Simplified RBAC + Direct Capability-Based Access Control (CBAC) + Resource Scoping** model:

```text
User
 ├── Exactly ONE Base Role (Persona & App Shell)
 │     ├── TENANT_ADMIN  (Tenant Administration Shell)
 │     ├── MANAGER       (Supervisory & Exception Overview Shell)
 │     └── STAFF         (Operational Work Shell)
 │
 └── N Direct User Permissions (Runtime Capability Authority)
       ├── route_planning:approve
       ├── mail:thread:reassign
       ├── ocr:review
       ├── compliance:override
       ├── billing_settlement:settlement:manage
       └── ...
```

### Core Architectural Invariants:
1. **`Role != Authority`**: Base Role defines the user's default dashboard layout and persona shell. Base Role is **never** evaluated as operational business authority.
2. **`Authority = Direct UserPermissions`**: Every backend action is evaluated strictly against the user's active direct capability permissions (e.g. `[RequirePermission(PermissionConstants.RoutePlanning.Approve)]`).
3. **`StaffType is 100% REMOVED`**: Legacy departmental staff enums (`Operations`, `Documentation`, `Finance`, `CustomerService`) do **not exist** in code, database, or proto. Specialization is achieved entirely through direct capabilities.
4. **`Zero System-Admin Escalation`**: Tenant Admins cannot assign `SYSTEM_ADMIN` role or system-only permissions (`mail:system:manage`, `compliance:platform:ingest`).

---

## 5. Base Role vs. Direct Capability UX

| Base Role Code | Display Name | UI Purpose & Default Persona | Baseline Permission Template |
| :--- | :--- | :--- | :--- |
| **`STAFF`** | Tenant Staff | Standard operational workspace ("My Work"). | Baseline shipment, route, mail, and rating operations. |
| **`MANAGER`** | Operations Manager | Supervisory overview and exception queues. | Extended with supervisory reassignment, approvals, and overrides. |
| **`TENANT_ADMIN`** | Tenant Administrator | Full administrative console across tenant services. | Full tenant-scoped management capabilities (IAM, Mail, Policies). |

### Role Change Semantics:
- Changing a user's Base Role from `STAFF` to `MANAGER` **preserves all existing direct permissions**.
- An optional toggle `"Apply Role Default Permissions"` performs an **idempotent union** of the new role's baseline template without wiping custom assigned capabilities.

---

## 6. Core Admin Resources

```text
Tenant (Organization)
  ├── User (Staff / Manager / Admin)
  │     ├── BaseRole (STAFF | MANAGER | TENANT_ADMIN)
  │     ├── UserStatus (Invited | Active | Suspended)
  │     ├── PermissionVersion (Concurrency & Cache Invalidation Token)
  │     └── UserPermission (Direct Capability Token)
  │
  ├── Role (Canonical Base Role Definitions & Templates)
  │
  ├── TenantAiConfig (Route Planning AI Automation Policy & Model Provider)
  │
  ├── TenantRuleConfig (Rule Engine Thresholds: HeavyWeight, MaxStops, etc.)
  │
  ├── RiskPolicy (Versioned Tenant Risk Policy: Draft -> Review -> Published)
  │
  └── KnowledgeDocument (Tenant-specific SOPs and operational guides)
```

---

## 7. Permission Domain Groups

Aurora organizes capability permissions into **10 cohesive business domains** (from `PermissionConstants.cs`):

```text
1. IAM & Access Control (iam:*)
   ├── iam:user:read              [Standard]    View staff directory & profiles
   ├── iam:user:invite            [Admin]       Invite new users to tenant
   ├── iam:user:update            [Admin]       Update profile, suspend/activate users
   ├── iam:role:read              [Standard]    Inspect base roles & templates
   ├── iam:role:manage            [Admin]       Change user base roles
   └── iam:permission:manage      [Admin]       Grant / revoke direct capabilities

2. Mail Platform (mail:*)
   ├── mail:read                  [Standard]    Access mail queues & threads
   ├── mail:draft:create          [Standard]    Create & edit draft messages
   ├── mail:send                  [Standard]    Submit outbound email
   ├── mail:thread:claim          [Standard]    Claim unassigned email threads
   ├── mail:thread:read_all       [Elevated]    View all staff threads (Supervision)
   ├── mail:thread:reassign       [Elevated]    Reassign threads between staff
   ├── mail:thread:unassign       [Elevated]    Release threads to unassigned queue
   ├── mail:quarantine:read       [Elevated]    View security quarantine list
   ├── mail:quarantine:release    [Elevated]    Release false-positive emails
   ├── mail:quarantine:delete     [Admin]       Permanently purge threat records
   ├── mail:audit:read            [Admin]       View mail security audit trail
   ├── mail:domain:manage         [Admin]       Provision domains & DKIM keys
   └── mail:mailbox:manage        [Admin]       Create shared mailboxes & aliases

3. Shipment & Logistics (shipments:*)
   ├── shipments:read             [Standard]    View shipments & milestones
   ├── shipments:create           [Standard]    Create new draft shipments
   ├── shipments:update           [Standard]    Update cargo items & locations
   ├── shipments:submit           [Standard]    Submit shipments for dispatch
   ├── shipments:import           [Elevated]    Bulk import shipments via CSV/Excel
   ├── shipments:cancel           [Elevated]    Cancel active shipments
   └── shipments:delete           [Admin]       Delete draft shipments

4. Route Planning & VRP (route_planning:*)
   ├── route_planning:read        [Standard]    View routes & stops
   ├── route_planning:create      [Standard]    Create draft routes
   ├── route_planning:update      [Standard]    Edit stops & parameters
   ├── route_planning:optimize    [Standard]    Execute VROOM VRP optimization
   ├── route_planning:execute     [Standard]    Dispatch approved routes
   ├── route_planning:delete      [Elevated]    Delete routes
   ├── route_planning:approval:read [Elevated]  View pending high-risk route queue
   ├── route_planning:approve     [Elevated]    Approve high-risk routes
   ├── route_planning:reject      [Elevated]    Reject high-risk routes
   ├── route_planning:policy:manage [Admin]     Configure rule thresholds & AI policy
   └── route_planning:policy:publish[Admin]     Publish new tenant risk policies

5. Document Processing & OCR (ocr:*)
   └── ocr:review                 [Elevated]    Review low-confidence OCR extractions

6. Documents & Knowledge (documents:*)
   ├── documents:ingest           [Elevated]    Upload custom SOPs & documents
   └── documents:manage           [Admin]       Manage knowledge lifecycle & versions

7. Regulatory Compliance (compliance:*)
   └── compliance:override        [Elevated]    Override customs compliance blocks

8. Financial Rating (financial_tax:*)
   ├── financial_tax:read         [Standard]    View freight cost matrix & tax rates
   └── financial_tax:calculate    [Standard]    Calculate freight & customs duty

9. Billing & Settlement (billing_settlement:*)
   ├── billing_settlement:read    [Standard]    View invoices & credit status
   ├── billing_settlement:credit:check [Standard] Perform customer credit aging check
   ├── billing_settlement:escrow:read  [Standard] View escrow wallet balance
   ├── billing_settlement:invoice:create [Elevated] Generate invoices from POD
   ├── billing_settlement:invoice:update [Elevated] Update invoice lines & status
   └── billing_settlement:settlement:manage [Admin] Release escrow & carrier funds

10. GPS Telematics & Geofencing (gps_tracking:*)
    └── gps_tracking:geofence:manage [Elevated] Create & edit geofence zones
```

---

## 8. Information Architecture (Admin Console)

```text
Tenant Admin Console
  │
  ├── 1. Overview (/admin/overview)
  │     ├── Tenant profile card & active plan tier
  │     ├── People metrics (Active Users, Pending Invitations, Suspended)
  │     ├── Risk & Governance summary (Elevated users, Active Policy version)
  │     └── Quick actions (Invite User, Configure Rules)
  │
  ├── 2. People & Access
  │     ├── Users (/admin/users)
  │     │     ├── User directory table (Search, Filter by Role/Status)
  │     │     ├── Invite User modal
  │     │     ├── User Detail & Capability Drawer
  │     │     └── Bulk Permission Update modal
  │     ├── Roles & Personas (/admin/roles)
  │     │     └── Canonical Base Role cards & baseline template explorer
  │     └── Capability Matrix (/admin/permissions)
  │           └── Searchable directory of all 35+ tenant capabilities
  │
  ├── 3. Operations Configuration
  │     ├── Route Risk Rules (/admin/config/route-rules)
  │     │     ├── Threshold editors for 7 canonical routing rules
  │     │     └── Rule activation toggles
  │     ├── AI Automation Policy (/admin/config/ai-policy)
  │     │     ├── Routing AI automation mode (Manual | RulesOnly | RulesAndLlm | RulesLlmApproval)
  │     │     └── Provider selection (Google Gemini | Azure OpenAI)
  │     └── Knowledge & SOP Ingestion (/admin/config/knowledge)
  │           ├── Tenant operational guides inventory
  │           └── "+ Upload SOP Document" modal
  │
  ├── 4. Mail Administration (/admin/mail) ──► (Links to Admin Mail Suite)
  │     ├── Overview, Domains, Shared Mailboxes, Aliases, Quarantine, Audit
  │
  └── 5. Audit & Security (/admin/audit)
        └── Tenant IAM & Configuration mutation audit log
```

---

## 9. Main Administrative Workflows

### Flow 1: Invite New User with Role Preset
1. Admin clicks **"+ Invite User"** (`/admin/users`).
2. Admin enters `Email`, `FirstName`, `LastName`, and selects Base Role (e.g. `STAFF`).
3. Toggle `"Apply Default Permissions"` is checked by default (pre-selects 10 baseline operational capabilities).
4. Admin optionally checks additional specific capabilities (e.g. `ocr:review`).
5. Admin submits form (`POST /api/v1/admin/staff`). Backend sends invitation and creates user in status `INVITED`.

### Flow 2: Granting / Revoking Direct Capabilities (Delta Semantics)
1. Admin opens a user's detail drawer and clicks **"Edit Direct Permissions"**.
2. UI displays permissions grouped by domain with search, filter, and risk tags.
3. Admin selects `route_planning:approve` (tagged **Elevated**).
4. System prompts with an **Elevated Permission Confirmation** explaining the operational authority granted.
5. Admin confirms. Backend executes delta update (`PATCH /api/v1/admin/staff/{id}/permissions` with `{ "grant": ["route_planning:approve"] }`).
6. `PermissionVersion` increments; user's active session receives updated capabilities in real time.

### Flow 3: Bulk Capability Assignment
1. Admin selects 5 staff members in the user table.
2. Admin clicks **"Manage Capabilities (5 Selected)"**.
3. Admin selects **Grant: `ocr:review`**.
4. Backend executes `PATCH /api/v1/admin/staff/permissions` applying delta grants without overwriting existing capabilities.

### Flow 4: Configure Route Planning Risk Rules
1. Admin navigates to **Route Risk Rules** (`/admin/config/route-rules`).
2. Admin adjusts `HeavyWeightRule` threshold from `10,000 kg` to `15,000 kg`.
3. Admin saves changes (`PUT /api/v1/admin/rule-configs/HeavyWeightRule`).
4. Rule cache invalidates across all backend route planning nodes.

---

## 10. Security & Tenant Boundaries

1. **System Permission Isolation**:
   - `mail:system:manage` (dead-letter requeuing) and `compliance:platform:ingest` (national law ingestion) are marked `SYSTEM_ONLY`.
   - The UI completely hides and excludes these permissions from tenant user assignment.
2. **Role Assignment Guardrail**:
   - `SYSTEM_ADMIN` role cannot be selected or assigned within tenant administration.
3. **Double-Layer Authorization**:
   - Every administrative action requires specific capability tokens (`iam:user:invite`, `iam:permission:manage`, `route_planning:policy:manage`).

---

## 11. Relationship to Mail Admin

The **Admin Operations Console** and **Admin Mail UI** share a unified enterprise navigation shell. The Mail section (`/admin/mail/*`) acts as a dedicated subsystem within the admin console, accessible when the admin possesses `mail:domain:manage` or `mail:mailbox:manage`.

---

## 12. MVP Scope (Current Supported vs. Target)

| Administrative Capability | Current MVP Status | UI Handling / Figma Note |
| :--- | :--- | :--- |
| **Invite Staff & User Listing** | `SUPPORTED_CURRENTLY` | Full table, pagination, invite modal. |
| **Update Profile (Name)** | `SUPPORTED_CURRENTLY` | Inline editing in detail drawer. |
| **Activate / Suspend User** | `SUPPORTED_CURRENTLY` | Status toggle with confirmation modal. |
| **Change Base Role** | `SUPPORTED_CURRENTLY` | Role selector with default permission union toggle. |
| **Direct Permission Delta Update**| `SUPPORTED_CURRENTLY` | Domain-grouped permission picker. |
| **Bulk Permission Delta Update** | `SUPPORTED_CURRENTLY` | Multi-select action bar & delta modal. |
| **Role Catalog & Templates** | `SUPPORTED_CURRENTLY` | Role definition cards & template viewer. |
| **Route Rule Threshold Config** | `SUPPORTED_CURRENTLY` | Interactive rule cards with threshold inputs. |
| **Tenant AI Automation Config** | `SUPPORTED_CURRENTLY` | Policy mode & provider selector. |
| **Permission Descriptions Endpoint**| `TARGET_SUPPORTED_BUT_API_MISSING` | UI embeds static descriptions from `PermissionConstants`. |
| **Risk Policy Version Lifecycle** | `TARGET_SUPPORTED_BUT_API_MISSING` | Draft/Publish lifecycle UI prepared for v1.1. |

---

## 13. Backend / API Gaps

1. **Permission Catalog Query (`GET /api/v1/admin/permissions`)**: The backend defines permissions in `PermissionConstants.cs` but does not expose a standalone metadata endpoint. The UI uses the client-side permission dictionary matching `PermissionConstants.GetAllPermissions()`.
2. **Risk Policy Draft & Publish Endpoints**: `RoutePlanningService` has gRPC RPCs (`CreateRiskPolicyDraft`, `PublishRiskPolicy`), but dedicated REST routes in `Admin.Bff` are scheduled for v1.1.
