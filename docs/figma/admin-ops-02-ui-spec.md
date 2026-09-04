# Aurora Tenant Admin Console — UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/admin-ops-01-product-context.md`  
> **Source of Truth:** Audited against `.NET 10` `IamTenant`, `Admin.Bff`, `RoutePlanningAgent`, `RegulatoryCompliance`, `MailService`, and `PermissionConstants.cs`.

---

## 1. Design Direction & System Tokens

- **Aesthetic:** Enterprise B2B, serious, high-density, security-conscious, modern SaaS control plane.
- **Reference Standards:** Cloudflare Dashboard, Microsoft Entra Admin Center, AWS IAM, Stripe Dashboard.
- **Grid System:** 8-point spatial grid (`8px`, `16px`, `24px`, `32px`, `48px`).
- **Primary Viewport:** Desktop-first (1440px primary, 1280px supported, 1024px minimum).

### Design Tokens
| Token Category | Value / Spec | Usage |
|---|---|---|
| **Typography** | `Inter`, `-apple-system`, `sans-serif` | Clean, highly legible UI font |
| **H1 (Page Title)** | `24px / 32px (Bold, SemiBold)` | Primary screen headers |
| **H2 (Section)** | `18px / 24px (SemiBold)` | Subsection and drawer headers |
| **Body (Default)** | `14px / 20px (Regular)` | Table cells, form labels, body text |
| **Body (Small/Mono)**| `12px / 16px (Mono / Medium)` | Permission codes, timestamps, IDs |
| **Primary Color** | `#2563EB` (Blue 600) | Brand primary, action buttons, active states |
| **Surface Colors** | `#FFFFFF` (Base), `#F8FAFC` (Canvas), `#F1F5F9` (Subtle) | Light mode background hierarchy |
| **Border Color** | `#E2E8F0` (Slate 200) | Data table dividers, card strokes |
| **Success Status** | `#10B981` (Emerald 500), bg `#ECFDF5` | `ACTIVE`, `SUCCESS`, `GRANTED`, `DEFAULT` |
| **Warning / Elevated**| `#F59E0B` (Amber 500), bg `#FFFBEB` | `INVITED`, `ELEVATED PERMISSION` |
| **Critical / Danger** | `#EF4444` (Red 500), bg `#FEF2F2` | `SUSPENDED`, `REVOKED`, `ADMIN ACTION` |

---

## 2. Unified Admin Console Layout & Navigation (1440px)

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
│ MAIL ADMIN   │                                                              │
│ • Overview   │                                                              │
│ • Domains    │                                                              │
│ • Mailboxes  │                                                              │
│ • Aliases    │                                                              │
│ • Quarantine │                                                              │
│ • Mail Audit │                                                              │
│              │                                                              │
│ AUDIT & SEC  │                                                              │
│ • Audit Log  │                                                              │
└──────────────┴──────────────────────────────────────────────────────────────┘
```

---

## 3. Screen 1: Admin Overview (`/admin/overview`)

**Purpose:** Executive summary of tenant organization health, team size, elevated access, policy versions, and mail summary.

### Metric Summary Cards (4 Columns)
| Card Title | Value | Subtext / Indicator | Click Action |
|---|---|---|---|
| **Total Users** | `24` | `21 Active • 2 Invited • 1 Suspended` | View Users → |
| **Elevated Capabilities**| `7` | `5 Users possess supervisory/approval rights` | Review Access → |
| **Active Route Policy** | `v2.1` | `Published on Aug 20, 2026 • 7 Rules Active` | Manage Rules → |
| **Shared Mailboxes** | `4` | `1 Default • 3 Specialized • 1 Domain Assigned`| Mail Admin → |

---

## 4. Screen 2: Users & Staff Management (`/admin/users`)

- **Table Columns:** Name, Email, Base Role (`STAFF` / `MANAGER` / `TENANT_ADMIN`), Status (`Active`, `Invited`, `Suspended`), Direct Capabilities Count, Last Active, Actions (`Edit Profile`, `Change Role`, `Manage Permissions`, `Deactivate`).
- **No `StaffType` Enums:** Do not design legacy department tags (Operations, Finance, CS). Direct permissions represent all specializations.
