# Aurora IAM — Frontend Authorization & Permission Guide

> **Status**: AUTHORITATIVE / TARGET ARCHITECTURE  
> **Audience**: Frontend Developers (React, Next.js, Vue, Mobile)

---

## 1. Overview & Golden Rules

In the Aurora frontend architecture:

1. **`ROLE` controls the Macro-Experience**:
   - Application shell layout
   - Top-level navigation items
   - Default dashboard views (e.g. `MANAGER` sees Supervisory Dashboard, `STAFF` sees My Work Dashboard)
2. **`PERMISSIONS` control Micro-Actions & Button Visibility**:
   - Showing or hiding action buttons (Approve, Reject, Reassign, Override, Release)
   - Enabling or disabling interactive controls
   - Route-level permission guards

```typescript
// ❌ WRONG: Inferred authority from role
if (user.role === 'MANAGER') {
  return <ApproveRouteButton onClick={handleApprove} />;
}

// ✅ CORRECT: Direct capability-based check
if (hasPermission('route_planning:approve')) {
  return <ApproveRouteButton onClick={handleApprove} />;
}
```

---

## 2. Frontend Authorization Store & Types

### 2.1 Current User Type Definition
On mount, the application invokes `GET /api/v1/auth/me`:

```typescript
export interface CurrentUser {
  userId: string;
  tenantId: string;
  email: string;
  name: string;
  role: 'SYSTEM_ADMIN' | 'TENANT_ADMIN' | 'MANAGER' | 'STAFF';
  permissions: string[]; // List of granular capability codes
  isAuthenticated: boolean;
}
```

### 2.2 Global Auth Context & React Hook (`useAuthorization`)
```typescript
import React, { createContext, useContext, useMemo } from 'react';

interface AuthContextValue {
  user: CurrentUser | null;
  hasPermission: (permission: string) => boolean;
  hasAnyPermission: (permissions: string[]) => boolean;
  hasAllPermissions: (permissions: string[]) => boolean;
  isRole: (role: CurrentUser['role']) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ user, children }: { user: CurrentUser | null; children: React.ReactNode }) {
  const permissionSet = useMemo(() => new Set(user?.permissions ?? []), [user?.permissions]);

  const value = useMemo<AuthContextValue>(() => ({
    user,
    hasPermission: (perm: string) => permissionSet.has(perm),
    hasAnyPermission: (perms: string[]) => perms.some(p => permissionSet.has(p)),
    hasAllPermissions: (perms: string[]) => perms.every(p => permissionSet.has(p)),
    isRole: (role: CurrentUser['role']) => user?.role === role,
  }), [user, permissionSet]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuthorization() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuthorization must be used within an AuthProvider');
  }
  return context;
}
```

---

## 3. Declarative UI Component Gates

### 3.1 `<PermissionGate>` Component
Use declarative wrappers instead of scattering inline conditionals:

```tsx
import React from 'react';
import { useAuthorization } from './useAuthorization';

interface PermissionGateProps {
  permission?: string;
  anyPermissions?: string[];
  allPermissions?: string[];
  fallback?: React.ReactNode;
  children: React.ReactNode;
}

export function PermissionGate({
  permission,
  anyPermissions,
  allPermissions,
  fallback = null,
  children,
}: PermissionGateProps) {
  const { hasPermission, hasAnyPermission, hasAllPermissions } = useAuthorization();

  let authorized = true;

  if (permission && !hasPermission(permission)) {
    authorized = false;
  }
  if (anyPermissions && !hasAnyPermission(anyPermissions)) {
    authorized = false;
  }
  if (allPermissions && !hasAllPermissions(allPermissions)) {
    authorized = false;
  }

  return authorized ? <>{children}</> : <>{fallback}</>;
}
```

### 3.2 Concrete UI Usage Examples

#### Example 1: Route Approval & Rejection
```tsx
import { PermissionGate } from '@/components/auth/PermissionGate';

export function RouteDetailActions({ routeId }: { routeId: string }) {
  return (
    <div className="flex gap-2">
      {/* Route Execution is available to anyone with execute capability */}
      <PermissionGate permission="route_planning:execute">
        <Button onClick={() => handleExecute(routeId)}>Execute Route</Button>
      </PermissionGate>

      {/* Approve button is strictly gated by route_planning:approve */}
      <PermissionGate permission="route_planning:approve">
        <Button variant="success" onClick={() => handleApprove(routeId)}>
          Approve High-Risk Route
        </Button>
      </PermissionGate>

      {/* Reject button is strictly gated by route_planning:reject */}
      <PermissionGate permission="route_planning:reject">
        <Button variant="danger" onClick={() => handleReject(routeId)}>
          Reject Route
        </Button>
      </PermissionGate>
    </div>
  );
}
```

#### Example 2: Shared Mail Inbox Triage
```tsx
export function MailThreadActions({ thread }: { thread: MailThreadDto }) {
  return (
    <div className="flex gap-2">
      {/* Claim thread for self */}
      {thread.status === 'UNASSIGNED' && (
        <PermissionGate permission="mail:thread:claim">
          <Button onClick={() => handleClaim(thread.id)}>Claim Thread</Button>
        </PermissionGate>
      )}

      {/* Reassign to another team member */}
      <PermissionGate permission="mail:thread:reassign">
        <Button variant="outline" onClick={() => openReassignModal(thread.id)}>
          Reassign Staff
        </Button>
      </PermissionGate>

      {/* Unassign back to general pool */}
      <PermissionGate permission="mail:thread:unassign">
        <Button variant="ghost" onClick={() => handleUnassign(thread.id)}>
          Unassign
        </Button>
      </PermissionGate>
    </div>
  );
}
```

#### Example 3: OCR Review & Compliance Override
```tsx
export function DocumentReviewSection({ ocrJobId }: { ocrJobId: string }) {
  return (
    <div>
      <PermissionGate permission="ocr:review" fallback={<p className="text-gray-500">Read-only view.</p>}>
        <OcrCorrectionEditor jobId={ocrJobId} />
      </PermissionGate>

      <PermissionGate permission="compliance:override">
        <Button variant="warning" onClick={() => openComplianceOverrideModal()}>
          Override Trade Compliance Warning
        </Button>
      </PermissionGate>
    </div>
  );
}
```

---

## 4. Role-Based Navigation & Layout Shell

Base Role is used to route users to the appropriate top-level navigation structure and persona dashboard:

```tsx
export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, isRole } = useAuthorization();

  return (
    <div className="layout-container">
      <Sidebar>
        {/* Navigation items adapted to persona */}
        {isRole('STAFF') && <StaffNavMenu />}
        {isRole('MANAGER') && <ManagerNavMenu />}
        {isRole('TENANT_ADMIN') && <AdminNavMenu />}
        {isRole('SYSTEM_ADMIN') && <SystemNavMenu />}
      </Sidebar>

      <main className="content-area">
        {children}
      </main>
    </div>
  );
}
```
