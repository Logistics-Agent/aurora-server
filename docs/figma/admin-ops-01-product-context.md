# Aurora Tenant Admin Console — Product Context

> **Design Target:** Figma AI / UI Designer Reference Specification  
> **Source of Truth:** Audited against `.NET 10` `IamTenant`, `Admin.Bff`, `RoutePlanningAgent`, `RegulatoryCompliance`, `MailService`, `PermissionConstants.cs`, and `protos/iam_tenant.proto`.

---

## 1. Product Summary

**Aurora** is an enterprise multi-tenant B2B SaaS logistics and freight execution platform. The **Aurora Admin Console** (`/admin/*`) is the unified organizational control plane for the Tenant Administrator (`TENANT_ADMIN`).

The Admin Console unifies four core administrative pillars within a coherent application shell:
1. **People & Access**: User identity lifecycle (Invited, Active, Suspended), canonical persona roles (`STAFF`, `MANAGER`, `TENANT_ADMIN`), and direct capability-based permissions.
2. **Operations Configuration**: Route dispatch risk rules, AI automation policies, and company operational guides (SOPs).
3. **Mail Administration**: Company mail domain visibility, shared department mailboxes, inbound forwarding aliases, security quarantine oversight, and mail audit trails.
4. **Audit & Security**: Immutable tenant-wide audit trails for security and IAM mutations.

The Admin Console is strictly separated from daily operational triage (`Staff.Bff` / Aurora Operations Workspace) and global platform multi-tenant administration (`System.Bff` / System Admin Control Plane).

---

## 2. Primary User Persona

**Persona:** `TENANT_ADMIN` / Organization IT Administrator & Operations Director

### Primary Goals:
- Onboard new staff members and grant precise operational capabilities.
- Audit and adjust direct capabilities for operational specialists (e.g. assigning route approval authority to a senior operator).
- Deactivate departing staff instantly across all tenant services.
- Tune logistical risk rules (e.g. maximum stop count, heavy cargo limits) without developer intervention.
- Configure AI automation levels (Manual, Rules-only, or Rules + LLM) for company workflows.
- Manage shared company mailboxes and forwarding aliases under system-assigned mail domains.

---

## 3. Administrative Persona Shell & Boundaries

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                             SYSTEM_ADMIN                                    │
│  - System.Bff: Tenant onboarding, plan tiers, global law ingestion          │
│  - Stalwart Admin UI: Mail domain provisioning & tenant assignment          │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Tenant Boundary
┌─────────────────────────────────────────────────────────────────────────────┐
│                 TENANT_ADMIN (Aurora Admin Console)                         │
│  ├── People & Access: Users, Roles, Direct Capability Grants                │
│  ├── Operations Config: Route risk rules, AI automation policy, SOPs        │
│  ├── Mail Administration: Assigned domains, shared mailboxes, aliases       │
│  └── Audit & Security: Tenant IAM mutations & security audit logs           │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Operational Queue
┌─────────────────────────────────────────────────────────────────────────────┐
│                      STAFF / MANAGER (Operations Workspace)                 │
│  - Shipments, Route Planning, OCR Documents, Compliance, GPS Tracking       │
│  - Collaborative Email Triage (UNASSIGNED, MY_WORK, ALL)                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Unified Sidebar Navigation Structure

```text
Aurora Admin
├── Overview
├── People & Access
│   ├── Users
│   ├── Roles & Personas
│   └── Capabilities
├── Operations Configuration
│   ├── Route Risk Rules
│   ├── AI Automation Policy
│   └── Knowledge & SOP
├── Mail Administration
│   ├── Mail Overview
│   ├── Domains
│   ├── Shared Mailboxes
│   ├── Aliases
│   ├── Quarantine
│   └── Mail Audit
└── Audit & Security
```

---

## 5. IAM & Authorization Invariants

1. **`Role != Authority`**: Base Role (`TENANT_ADMIN`, `MANAGER`, `STAFF`) defines the user's default dashboard layout and persona shell. Base Role is **never** evaluated as operational business authority.
2. **`Authority = Direct UserPermissions`**: Every backend action is evaluated strictly against the user's active direct capability permissions (e.g. `[RequirePermission(PermissionConstants.RoutePlanning.Approve)]`).
3. **`StaffType is 100% REMOVED`**: Legacy departmental staff enums (`Operations`, `Documentation`, `Finance`, `CustomerService`) do **not exist** in code, database, or proto. Specialization is achieved entirely through direct capabilities.
4. **`Zero System-Admin Escalation`**: Tenant Admins cannot assign `SYSTEM_ADMIN` role or system-only permissions (`mail:system:manage`, `compliance:platform:ingest`).
