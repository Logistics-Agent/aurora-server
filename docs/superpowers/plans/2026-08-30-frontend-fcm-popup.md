# Frontend FCM Popup Implementation Plan

**Goal:** Kết nối Aurora FE với Staff BFF để đăng ký FCM Web token, nhận popup foreground/background, đồng bộ notification history/unread state và điều hướng an toàn tới shipment hoặc notification detail.

**Architecture:** FE chỉ gọi YARP/Staff BFF bằng cookie session; FE không gọi Notification gRPC và không bao giờ nhận Firebase Admin JSON. Firebase Web SDK dùng public web config và VAPID key ở client, còn Notification service dùng Admin JSON ở backend. FCM lifecycle được khởi tạo sau khi user authenticated và có permission notifications:access; token được đăng ký qua BFF, foreground message hiện Sonner toast, background message đi qua service worker.

**Tech Stack:** Next.js 16 App Router, React 19, strict TypeScript, Firebase Web Messaging, Axios, TanStack Query v5, Zod, Sonner, Vitest, Testing Library.

**Spec:** /home/kaito/project/aurora-server/docs/technical/frontend/FE_INTEGRATION_GUIDE.md, /home/kaito/project/aurora-server/docs/technical/notification-spec.md, /home/kaito/project/aurora-server/docs/documents/events/notification-events.md, /home/kaito/project/aurora-client/AGENTS.md, /home/kaito/project/aurora-client/logistics_control_tower_nextjs_feature_architecture_pack_v3/rules.md

**Implementation status:** Tasks 0–9 are implemented in the FE working tree and
verified with typecheck, lint, 42 test files/187 tests, production build and
diff checks. The developer-local .env.local was intentionally not created
because this workspace does not contain the Firebase Web public config or VAPID
key. Task 10 remains a manual runtime acceptance step: it needs authenticated
Gateway/BFF access, a browser FCM token, the backend Firebase Admin credential
and a real producer event.

## Global Constraints

- Mọi browser request đi qua NEXT_PUBLIC_API_BASE_URL và Staff BFF; không gọi localhost:6001, gRPC hoặc Firebase Admin trực tiếp từ FE.
- Mọi notification route yêu cầu cookie session authenticated và direct permission notifications:access; không bypass auth bằng mock user hoặc client-supplied TenantId/UserId.
- Firebase Admin service-account JSON, private key và server service key chỉ nằm ở Notification backend; tuyệt đối không copy sang /home/kaito/project/aurora-client.
- Firebase Web config và VAPID key là public client configuration, nhưng vẫn lấy từ environment; không hard-code environment-specific values vào source.
- Chỉ yêu cầu browser permission sau thao tác rõ ràng của user, không gọi Notification.requestPermission() khi page vừa load.
- Không lưu FCM registration token vào localStorage, Zustand hoặc log; chỉ lưu device id do BFF trả về nếu cần disable/logout.
- TanStack Query là server state; không mirror notification list vào Zustand. Query/mutation hooks phải nằm dưới src/hooks/queries/<domain>/ và src/hooks/mutations/<domain>/.
- Shared app logic belongs in src/types, src/dto, src/lib, src/utils or src/constants only after genuine reuse across independent features; feature-wide logic belongs in src/features/<feature>; each sub-feature may own its own types, lib, utils, constants, components, hooks, dto, schemas and index.tsx.
- Keep every concern at the narrowest owner: src/features/notifications for notification-wide behavior, and src/features/notifications/<sub-feature> for popup/list/device-specific behavior. Index files must compose their page/workflow or orchestration, not be passive re-exports.
- src/app/**/page.tsx tiếp tục là route adapter mỏng; UI/workflow nằm trong src/features/notifications.
- Chỉ cho phép action path nội bộ /notifications hoặc /shipments/<guid>; tuyệt đối không điều hướng theo URL ngoài hoặc protocol-relative URL.
- Không commit .env.local, Firebase secrets, token thật hoặc browser credentials. Trong turn triển khai không tự động commit.

## Current State and Scope Boundary

Đã kiểm tra source hiện tại:

- /home/kaito/project/aurora-client/src/features/notifications/notification-center/index.tsx chỉ render notificationMocks và mark-read local.
- /home/kaito/project/aurora-client/src/features/notifications/mock/index.ts là fixture UI-only.
- /home/kaito/project/aurora-client/src/components/layout/app-header.tsx có bell tĩnh, chưa gọi unread API.
- /home/kaito/project/aurora-client/src/api/client/axios-client.ts chưa bật withCredentials và chưa có notification service/query.
- /home/kaito/project/aurora-client/src/features/auth/login/index.tsx đang xác thực mock, nên không thể lấy cookie BFF để gọi notification API.
- package.json chưa có firebase; .env.example chưa có Firebase Web variables; chưa có service worker.

Backend contract cần giữ nguyên:

~~~text
POST   /api/v1/notifications/devices
DELETE /api/v1/notifications/devices/{id}
POST   /api/v1/notifications/subscriptions/shipments/{shipmentId}
GET    /api/v1/notifications?page=1&pageSize=20&unreadOnly=false
GET    /api/v1/notifications/unread-count
PATCH  /api/v1/notifications/{id}/read
PATCH  /api/v1/notifications/read-all
~~~

Device request:

~~~json
{ "token": "<browser FCM token>", "platform": "Web", "appVersion": "<app version>" }
~~~

FCM data contract:

~~~json
{
  "notificationId": "uuid",
  "type": "SHIPMENT_DELIVERED",
  "shipmentId": "uuid",
  "actionUrl": "/shipments/uuid"
}
~~~

Notification list item contract:

~~~json
{
  "id": "uuid",
  "eventType": "SHIPMENT_DELIVERED",
  "channel": "FCM",
  "title": "Shipment delivered",
  "body": "Shipment SHP-001 was delivered.",
  "isRead": false,
  "createdAt": "2026-08-30T00:00:00Z",
  "readAt": null,
  "shipmentId": "uuid",
  "shipmentNumber": "SHP-001",
  "actionUrl": "/shipments/uuid"
}
~~~

---

### Task 0: Update FE architecture rules for nested feature ownership

**Files:**
- Modify: /home/kaito/project/aurora-client/AGENTS.md
- Modify: /home/kaito/project/aurora-client/logistics_control_tower_nextjs_feature_architecture_pack_v3/rules.md

- [x] **Step 1: Add the ownership policy to both FE rule sources.**

Document one consistent policy for all features:

~~~text
src/types, src/dto, src/lib, src/utils and src/constants contain logic shared by independent features.
src/features/<feature> owns behavior shared by its sub-features, including feature types, lib, utils, constants, components, hooks, dto and schemas.
src/features/<feature>/<sub-feature> owns behavior specific to that sub-feature and may contain its own types, lib, utils, constants, components, hooks, dto, schemas, stores and index.tsx.
Keep code at the narrowest owner and promote only after real reuse.
Each index.tsx composes a page/workflow or owns orchestration; it is not a passive re-export.
~~~

- [x] **Step 2: Verify existing features still follow the rule.**

Run from the FE repository:

~~~bash
cd /home/kaito/project/aurora-client
pnpm test -- src/features/feature-architecture.test.ts
pnpm lint
~~~

Record any pre-existing violations before changing feature code. Do not move unrelated files as part of the FCM popup work.

---

### Task 1: Establish FE dependency, environment and authenticated HTTP boundary

**Files:**
- Modify: /home/kaito/project/aurora-client/package.json
- Modify: /home/kaito/project/aurora-client/pnpm-lock.yaml
- Modify: /home/kaito/project/aurora-client/.env.example
- Create: /home/kaito/project/aurora-client/.env.local (local only, never commit)
- Modify: /home/kaito/project/aurora-client/src/configs/env.config.ts
- Modify: /home/kaito/project/aurora-client/src/api/client/axios-client.ts
- Test: /home/kaito/project/aurora-client/src/configs/env.config.test.ts
- Test: /home/kaito/project/aurora-client/src/api/client/axios-client.test.ts

**Interfaces:**
- Produces env.firebase with enabled, public Firebase config and vapidKey.
- Produces an apiClient that sends the BFF cookie session with withCredentials: true.
- Consumes local API gateway URL, not the Notification service URL.

- [x] **Step 1: Add the Firebase Web SDK using the project package manager.**

~~~bash
cd /home/kaito/project/aurora-client
pnpm add firebase
~~~

Record the resolved version in package.json and pnpm-lock.yaml. Do not install a browser Admin SDK.

- [x] **Step 2: Extend .env.example with public client variables.**

~~~dotenv
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
NEXT_PUBLIC_APP_NAME=Logistics AI Control Tower
NEXT_PUBLIC_FIREBASE_ENABLED=false
NEXT_PUBLIC_FIREBASE_API_KEY=
NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN=
NEXT_PUBLIC_FIREBASE_PROJECT_ID=
NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET=
NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID=
NEXT_PUBLIC_FIREBASE_APP_ID=
NEXT_PUBLIC_FIREBASE_VAPID_KEY=
~~~

Create .env.local only on the developer machine. For local popup testing set NEXT_PUBLIC_FIREBASE_ENABLED=true, fill the six public Firebase Web config values and the Web Push certificate/VAPID key from the same Firebase project. Do not put the backend Admin JSON or ServiceAuth__ApiKey in this file.

- [x] **Step 3: Implement typed environment parsing without breaking builds when FCM is disabled.**

In src/configs/env.config.ts retain the current app fields and add a firebase object with enabled, apiKey, authDomain, projectId, storageBucket, messagingSenderId, appId and vapidKey. Parse enabled from the literal string true. Add a refinement requiring every Firebase field and vapidKey to be non-empty only when enabled is true. When disabled, empty values must be accepted so CI and developers without Firebase credentials can still build.

- [x] **Step 4: Make Axios use the BFF cookie session.**

Update the Axios instance:

~~~ts
withCredentials: true,
headers: {
  "Content-Type": "application/json",
  Accept: "application/json",
},
~~~

Keep the existing ApiError mapping. Add a browser-only 401 redirect to the BFF login route, except when the current path already starts with /login. Build the return URL from pathname and search. Do not add an Authorization token from localStorage.

- [x] **Step 5: Write and run config/transport tests.**

Cover disabled Firebase with empty values, enabled Firebase with a missing field, apiClient credentials/Accept defaults, one browser 401 redirect, and unchanged 403/409/500 ApiError codes.

~~~bash
cd /home/kaito/project/aurora-client
pnpm vitest run src/configs/env.config.test.ts src/api/client/axios-client.test.ts
~~~

Expected: all new tests pass and no Firebase value appears in test output.

---

### Task 2: Define the notification REST service and server-state contracts

**Files:**
- Create: /home/kaito/project/aurora-client/src/api/services/notifications.ts
- Create: /home/kaito/project/aurora-client/src/api/services/notifications.schemas.ts
- Modify: /home/kaito/project/aurora-client/src/api/index.ts
- Modify: /home/kaito/project/aurora-client/src/api/query-keys/index.ts
- Create: /home/kaito/project/aurora-client/src/hooks/queries/notifications/use-notifications-query.ts
- Create: /home/kaito/project/aurora-client/src/hooks/queries/notifications/use-unread-notification-count-query.ts
- Create: /home/kaito/project/aurora-client/src/hooks/mutations/notifications/use-notification-mutations.ts
- Test: /home/kaito/project/aurora-client/src/api/services/notifications.test.ts
- Test: /home/kaito/project/aurora-client/src/hooks/queries/notifications/use-notifications-query.test.tsx
- Test: /home/kaito/project/aurora-client/src/hooks/mutations/notifications/use-notification-mutations.test.tsx

**Interfaces:**

~~~ts
export type NotificationRecord = {
  id: string;
  eventType: string;
  channel: string;
  title: string;
  body: string;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
  shipmentId: string | null;
  shipmentNumber: string | null;
  actionUrl: string | null;
};

export type NotificationListParams = {
  page?: number;
  pageSize?: number;
  unreadOnly?: boolean;
};

export type NotificationListResponse = {
  notifications: NotificationRecord[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export type RegisterDeviceRequest = {
  token: string;
  platform: "Web";
  appVersion: string;
};

export type DeviceResponse = {
  id: string;
  platform: string;
  isActive: boolean;
};
~~~

- [x] **Step 1: Add Zod schemas at the API boundary.**

Validate list and device responses before exposing them to UI. Accept readAt, shipmentId, shipmentNumber and actionUrl as nullable. Convert malformed server payloads to an ApiError-compatible validation error rather than rendering unknown objects.

- [x] **Step 2: Implement the REST service with exact BFF routes.**

Implement:

~~~ts
listNotifications(params: NotificationListParams): Promise<NotificationListResponse>
getUnreadNotificationCount(): Promise<number>
registerNotificationDevice(input: RegisterDeviceRequest): Promise<DeviceResponse>
removeNotificationDevice(id: string): Promise<void>
subscribeToShipment(shipmentId: string): Promise<void>
markNotificationRead(id: string): Promise<void>
markAllNotificationsRead(): Promise<number>
~~~

Use apiClient.get/post/delete/patch, query names page, pageSize and unreadOnly, and encoded path IDs. Never add tenantId or userId to any request body or query.

- [x] **Step 3: Add stable query keys.**

Extend src/api/query-keys/index.ts with a notifications namespace containing all, list(params) and unreadCount keys. Normalize default parameters so omitted values and explicit defaults do not create duplicate cache entries.

- [x] **Step 4: Add TanStack Query hooks.**

Use useQuery for list/unread count and useMutation for writes. Subscribe, mark-read and mark-all success invalidate the relevant list/unread keys. Device registration returns the device id to the FCM lifecycle; device removal clears the local device id after 204. Keep these hooks under src/hooks, not src/features/notifications.

- [x] **Step 5: Test request shape and cache invalidation.**

Assert exact routes/body/query, absence of tenant/user fields, response parsing and invalidation of list plus unread-count after read mutations. Cover 401, 403, 404 and 409.

~~~bash
cd /home/kaito/project/aurora-client
pnpm vitest run src/api/services/notifications.test.ts src/hooks/queries/notifications src/hooks/mutations/notifications
~~~

---

### Task 3: Implement Firebase Web initialization and root service-worker route

**Files:**
- Create: /home/kaito/project/aurora-client/src/features/notifications/lib/firebase-client.ts
- Create: /home/kaito/project/aurora-client/src/features/notifications/utils/fcm-payload.ts
- Create: /home/kaito/project/aurora-client/src/app/firebase-messaging-sw.js/route.ts
- Test: /home/kaito/project/aurora-client/src/features/notifications/lib/firebase-client.test.ts
- Test: /home/kaito/project/aurora-client/src/features/notifications/utils/fcm-payload.test.ts
- Test: /home/kaito/project/aurora-client/src/app/firebase-messaging-sw.js/route.test.ts

**Interfaces:**

~~~ts
export function isFcmSupported(): Promise<boolean>;
export function getFirebaseMessaging(): Promise<Messaging | null>;
export function registerFirebaseServiceWorker(): Promise<ServiceWorkerRegistration>;
export function readFcmPayload(payload: MessagePayload): FcmPayload | null;
export function safeNotificationPath(value: string | undefined): string | null;
~~~

- [x] **Step 1: Create a browser-only Firebase singleton.**

In firebase-client.ts guard all Firebase Messaging access with typeof window !== "undefined" and env.firebase.enabled. Use the modular Firebase Web API from firebase/app and firebase/messaging. Cache one FirebaseApp and one Messaging instance; call isSupported before getMessaging. Return null for disabled config, unsupported browsers or missing service-worker support without crashing unrelated rendering.

Initialization shape:

~~~ts
const app = initializeApp({
  apiKey: env.firebase.apiKey,
  authDomain: env.firebase.authDomain,
  projectId: env.firebase.projectId,
  storageBucket: env.firebase.storageBucket,
  messagingSenderId: env.firebase.messagingSenderId,
  appId: env.firebase.appId,
});
~~~

- [x] **Step 2: Register the same-origin service worker.**

Register exactly /firebase-messaging-sw.js with scope /. Wait for registration.active before getToken. Catch registration errors and expose a user-safe error state without logging token or Firebase config.

- [x] **Step 3: Implement payload parsing and internal navigation validation.**

Define:

~~~ts
export type FcmPayload = {
  notificationId: string;
  type: string;
  shipmentId: string | null;
  actionUrl: string | null;
  title: string;
  body: string;
};
~~~

readFcmPayload requires non-empty notificationId and type, takes title/body from payload.notification with safe fallback strings, and normalizes missing shipmentId/actionUrl to null. Validate before router.push, clients.openWindow or a toast action:

~~~ts
const internalPathPattern =
  /^\/(?:notifications(?:[/?#].*)?|shipments\/[0-9a-fA-F-]{36}(?:[/?#].*)?)$/;

export function safeNotificationPath(value: string | undefined): string | null {
  if (!value || !internalPathPattern.test(value)) return null;
  return value;
}
~~~

Reject https URLs, protocol-relative URLs, backslashes, javascript URLs, malformed IDs and arbitrary paths. Test valid and malicious values.

- [x] **Step 4: Generate the service-worker JavaScript from a same-origin Next route.**

Create src/app/firebase-messaging-sw.js/route.ts returning Content-Type application/javascript and Cache-Control no-store. The generated script may contain only public Web config. It must import pinned Firebase compat app/messaging scripts, initialize Firebase, handle background payloads, and handle notificationclick.

If a notification payload is already present, do not call showNotification a second time because the browser may auto-display it. The click handler focuses an existing same-origin client when possible, otherwise opens the safe action path. Invalid or absent actionUrl falls back to /notifications.

Do not use Admin SDK, service-account JSON, server key or private environment variables in this route. When FCM is disabled/missing, return a controlled no-op script or 404; client registration must handle that state without application crash.

- [x] **Step 5: Test browser/environment boundaries.**

Cover:

~~~text
server-side import does not access window, Notification or navigator
disabled Firebase returns null/no-op
unsupported browser returns null
valid FCM payload is parsed
malicious actionUrl is rejected
service-worker response has JavaScript content type and no Admin/private-key marker
~~~

Run:

~~~bash
cd /home/kaito/project/aurora-client
pnpm vitest run src/features/notifications/lib src/features/notifications/utils src/app/firebase-messaging-sw.js
~~~

---

### Task 4: Implement authenticated token registration and token lifecycle

**Files:**
- Create: /home/kaito/project/aurora-client/src/features/notifications/hooks/use-fcm-notification.ts
- Create: /home/kaito/project/aurora-client/src/features/notifications/components/fcm-permission-control.tsx
- Modify: /home/kaito/project/aurora-client/src/features/notifications/index.tsx
- Create: /home/kaito/project/aurora-client/src/features/notifications/lib/device-storage.ts
- Test: /home/kaito/project/aurora-client/src/features/notifications/hooks/use-fcm-notification.test.tsx
- Test: /home/kaito/project/aurora-client/src/features/notifications/lib/device-storage.test.ts
- Create: /home/kaito/project/aurora-client/src/hooks/mutations/auth/use-auth-logout.ts

**Interfaces:**

~~~ts
export type FcmRegistrationState =
  | "disabled"
  | "unsupported"
  | "idle"
  | "requesting"
  | "registering"
  | "enabled"
  | "denied"
  | "error";

export type UseFcmNotificationResult = {
  state: FcmRegistrationState;
  errorMessage: string | null;
  deviceId: string | null;
  enable: () => Promise<void>;
  refreshToken: () => Promise<void>;
  disable: () => Promise<boolean>;
};
~~~

- [x] **Step 1: Store only the BFF device id.**

Use a namespaced key such as aurora.notification.deviceId. Store the returned device id only after POST /devices succeeds. Never store or print the FCM token. Clear malformed stored values.

- [x] **Step 2: Implement explicit permission enablement.**

enable() runs only from the click handler of FcmPermissionControl:

~~~text
Firebase disabled => disabled
browser unsupported => unsupported
request Notification permission
permission denied => denied and no getToken
register service worker
getToken(messaging, { vapidKey, serviceWorkerRegistration })
empty token => error and no API call
POST /api/v1/notifications/devices with platform Web
store returned device id
enabled
~~~

Display clear states: Enable browser notifications, Notifications enabled, Permission blocked in browser settings, Browser not supported and a retry action for transient failures. Never expose token or Firebase exception details in UI.

- [x] **Step 3: Refresh token on authenticated app bootstrap.**

When permission is already granted, call refreshToken once per authenticated browser session. Firebase Web does not require a deprecated onTokenRefresh API; call getToken on bootstrap and re-register the current token. Use a ref or module-level in-flight promise to prevent duplicate calls caused by React Strict Mode.

- [x] **Step 4: Disable the device on explicit logout/disable.**

disable() calls DELETE /api/v1/notifications/devices/{deviceId} when a stored id exists, then clears it. A 404 clears local state because the device is already inactive; other errors retain the id and show a safe error. Device state is scoped to the authenticated user id, and the FE logout mutation completes device cleanup before calling BFF logout; failed cleanup blocks logout so a previous account cannot remain subscribed silently. Do not silently disable on every tab close.

- [x] **Step 5: Add a permission control to the Notification Center.**

Render it only for authenticated users with notifications:access. It must not request permission during render. Keep the page usable when FCM is disabled; disabled configuration is not a page error.

- [x] **Step 6: Test lifecycle behavior.**

Mock Firebase and the API service to verify disabled config, denied permission, granted permission, empty token, Strict Mode duplicate prevention, disable/delete and BFF 409 handling. Assert that token values never appear in rendered UI or logger calls.

---

### Task 5: Add foreground popup, background click handling and query synchronization

**Files:**
- Create: /home/kaito/project/aurora-client/src/features/notifications/popup/components/notification-fcm-bootstrap.tsx
- Create: /home/kaito/project/aurora-client/src/features/notifications/popup/lib/notification-toast.ts
- Modify: /home/kaito/project/aurora-client/src/app/(dashboard)/layout.tsx
- Modify: /home/kaito/project/aurora-client/src/providers/app-provider.tsx only if QueryClient access requires it
- Test: /home/kaito/project/aurora-client/src/features/notifications/popup/components/notification-fcm-bootstrap.test.tsx
- Test: /home/kaito/project/aurora-client/src/features/notifications/popup/lib/notification-toast.test.ts

**Interfaces:**

~~~ts
export function NotificationFcmBootstrap(props: {
  canReceiveNotifications: boolean;
}): React.JSX.Element | null;
export function showNotificationToast(
  payload: FcmPayload,
  onOpen: (path: string) => void,
): void;
~~~

- [x] **Step 1: Mount the FCM bootstrap inside the authenticated dashboard layout.**

Add the bootstrap to /home/kaito/project/aurora-client/src/app/(dashboard)/layout.tsx. It returns null for unauthenticated users, missing permission, disabled config or unsupported browsers; attaches one foreground onMessage listener after messaging is available; calls refreshToken only when Notification.permission is granted; listens for the first-time registration event so default → granted permission starts the listener without a full reload; and unsubscribes on unmount.

Do not mount it in the public auth layout and do not request permission automatically.

- [x] **Step 2: Render a foreground Sonner toast from the backend payload.**

Use payload.notification.title/body plus parsed payload.data. The toast action calls safeNotificationPath before router.push; invalid paths show no navigation action. Use the existing Toaster from src/providers/app-provider.tsx; do not add another toast provider.

~~~ts
toast(payload.title, {
  description: payload.body,
  action: safePath
    ? { label: "Open", onClick: () => onOpen(safePath) }
    : undefined,
});
~~~

- [x] **Step 3: Synchronize server state after a foreground message.**

After showing the toast, invalidate the default first-page notification list key and unread-count key. Do not construct a fake persisted row from the FCM payload; the list API remains the source of truth.

- [x] **Step 4: Handle background notifications in the service worker.**

When the browser is backgrounded, exactly one OS/browser notification must appear. Verify that the backend combined notification + data payload does not duplicate. The click handler focuses or opens a same-origin internal path.

- [x] **Step 5: Test foreground behavior.**

Assert one toast for a valid message, no action for an unsafe path, list/unread invalidation, unsubscribe on unmount and no listener for unauthenticated/no-permission state.

---

### Task 6: Replace notification mocks with live history and unread actions

**Files:**
- Modify: /home/kaito/project/aurora-client/src/features/notifications/notification-center/index.tsx
- Modify: /home/kaito/project/aurora-client/src/features/notifications/index.tsx
- Modify: /home/kaito/project/aurora-client/src/components/layout/app-header.tsx
- Create: /home/kaito/project/aurora-client/src/features/notifications/components/notification-list.tsx
- Create: /home/kaito/project/aurora-client/src/features/notifications/components/notification-empty-state.tsx
- Create: /home/kaito/project/aurora-client/src/components/layout/notification-bell.tsx
- Test: /home/kaito/project/aurora-client/src/features/notifications/notification-center/notification-center.test.tsx
- Test: /home/kaito/project/aurora-client/src/components/layout/notification-bell.test.tsx

**Interfaces:**

~~~ts
export type NotificationListProps = {
  notifications: NotificationRecord[];
  onMarkRead: (id: string) => void;
  onOpen: (actionUrl: string | null) => void;
};

export function NotificationBell(): React.JSX.Element;
~~~

- [x] **Step 1: Replace notificationMocks in the staff Notification Center.**

Use useNotificationsQuery with page 1, pageSize 20 and unreadOnly false. Render loading, error, empty and populated states. Show server fields createdAt, shipmentNumber, eventType, isRead, title and body.

- [x] **Step 2: Wire mark-read and mark-all-read mutations.**

Clicking a notification validates actionUrl and navigates only if safe. Mark read calls PATCH /{id}/read; Mark all read calls PATCH /read-all. Do not mark a notification read merely because it rendered.

- [x] **Step 3: Replace the static header bell with live unread count.**

NotificationBell uses useUnreadNotificationCountQuery, renders no badge for zero, renders 99+ above 99 and links to /notifications. Remove the hard-coded red dot/mock count.

- [x] **Step 4: Keep customer portal fixtures separate.**

Do not reuse the staff Notification BFF API in customer portal pages unless a customer notification contract exists. Keep the customer mock feature and avoid cross-feature imports.

- [x] **Step 5: Remove only obsolete staff mock usage.**

Delete the import/use of notificationMocks from the live staff Notification Center after live query rendering. Keep or remove the fixture file only after checking its tests; do not delete unrelated customer fixtures.

- [x] **Step 6: Test live UI state transitions.**

Cover loading, API error/retry, empty list, server rendering, mark-one, mark-all, unsafe actionUrl and live bell count/link behavior.

---

### Task 7: Connect real auth bootstrap and permission gating

**Files:**
- Create: /home/kaito/project/aurora-client/src/api/services/auth.ts
- Create: /home/kaito/project/aurora-client/src/hooks/queries/auth/use-current-user-query.ts
- Create: /home/kaito/project/aurora-client/src/types/auth.types.ts
- Create: /home/kaito/project/aurora-client/src/hooks/mutations/auth/use-auth-logout.ts
- Modify: /home/kaito/project/aurora-client/src/features/auth/login/index.tsx
- Modify: /home/kaito/project/aurora-client/src/app/(dashboard)/layout.tsx
- Modify: /home/kaito/project/aurora-client/src/features/notifications/index.tsx
- Test: /home/kaito/project/aurora-client/src/api/services/auth.test.ts
- Test: /home/kaito/project/aurora-client/src/hooks/queries/auth/use-current-user-query.test.tsx
- Test: /home/kaito/project/aurora-client/src/features/auth/login/login.test.tsx

**Interfaces:**

~~~ts
export type UserProfile = {
  userId: string;
  tenantId: string;
  email: string;
  name: string;
  role: "SYSTEM_ADMIN" | "TENANT_ADMIN" | "MANAGER" | "STAFF";
  permissions: string[];
  isAuthenticated: boolean;
};

export function hasPermission(
  user: UserProfile | null,
  permission: string,
): boolean;
~~~

- [x] **Step 1: Add GET /api/v1/auth/me service/query.**

Use apiClient.get<UserProfile>, parse the response, and treat 401 as unauthenticated rather than retrying forever. Use a stable auth.currentUser query key.

- [x] **Step 2: Replace fake login submit behavior with the BFF login redirect.**

The login form may retain visual fields, but submit must navigate to the BFF auth route:

~~~ts
const returnUrl = encodeURIComponent("/dashboard");
window.location.assign(
  env.NEXT_PUBLIC_API_BASE_URL +
    "/api/v1/auth/login?returnUrl=" +
    returnUrl,
);
~~~

Remove authenticateMock from the production login path. Do not collect or send Cognito password to FE API code.

- [x] **Step 3: Gate FCM and notification APIs by direct permission.**

Only initialize FCM, render the enable control or call notification endpoints when hasPermission(user, "notifications:access") is true. Missing permission renders the existing PermissionState/forbidden state and must not trigger repeated 403 calls.

- [x] **Step 4: Verify backend auth configuration before runtime test.**

The FE cannot make authenticated popup calls until Staff BFF has valid Cognito/OIDC configuration and the browser receives its HTTP-only cookie. Fix this in environment/auth setup, never by weakening FE or backend authorization.

- [x] **Step 5: Test auth/permission behavior.**

Assert login redirect, no FCM for unauthenticated users, no device POST without notifications:access, successful enable for permitted users, 401 redirect and 403 permission state.

---

### Task 8: Add shipment subscription UX at the business boundary

**Files:**
- Create: /home/kaito/project/aurora-client/src/features/shipment/shipment-detail/components/shipment-notification-subscription.tsx
- Modify: /home/kaito/project/aurora-client/src/features/shipment/shipment-detail/index.tsx
- Test: /home/kaito/project/aurora-client/src/features/shipment/shipment-detail/components/shipment-notification-subscription.test.tsx

**Interfaces:**

~~~ts
export type ShipmentNotificationSubscriptionProps = {
  shipmentId: string;
};
~~~

- [x] **Step 1: Add a subscription control to authenticated shipment detail.**

Call subscribeToShipment(shipmentId) through the mutation hook, never send tenant/user fields, show pending/success/error state and hide it without notifications:access.

- [x] **Step 2: Prevent duplicate subscription calls.**

Disable while pending and invalidate notification list after success. The backend may make the relation idempotent; FE still prevents double clicks.

- [x] **Step 3: Test route ID and authorization.**

Verify encoded shipment ID, no request without permission and safe rendering of BFF 403 instead of fake success.

---

### Task 9: Run static checks, tests and security checks before runtime proof

**Files:**
- Modify: /home/kaito/project/aurora-client/README.md
- Modify: /home/kaito/project/aurora-client/.gitignore only if .env.local is not already ignored
- Test: all FE files created/modified in Tasks 1–8

- [x] **Step 1: Add the FE runbook.**

Document:

~~~text
cp .env.example .env.local
fill only NEXT_PUBLIC_FIREBASE_* Web config and VAPID key
set NEXT_PUBLIC_API_BASE_URL to the local API Gateway URL
start Notification on :6001 with backend Admin JSON and ServiceAuth key
start Staff BFF with Redis__Host and matching Notification service key
start API Gateway if FE uses :5000
start FE with pnpm dev
login through BFF, grant browser permission, register device
~~~

State explicitly that Firebase Admin JSON belongs only in the server repository ignored secrets/firebase path and is never copied into FE.

- [x] **Step 2: Verify no secret/token leakage.**

Run:

~~~bash
cd /home/kaito/project/aurora-client
rtk rg -n "private_key|client_email|serviceAccount|ServiceAuth__ApiKey|local-notification-key|eyJ|FCM_TOKEN" . --glob '!pnpm-lock.yaml' --glob '!node_modules'
rtk git check-ignore -v .env.local
~~~

Expected: no credential/token matches and .env.local is ignored. Do not print .env.local contents.

- [x] **Step 3: Run complete FE quality gates.**

~~~bash
cd /home/kaito/project/aurora-client
pnpm lint
pnpm typecheck
pnpm test
pnpm build
~~~

Expected: zero lint/type/build errors and all tests pass. Existing unrelated warnings must be recorded, not hidden.

- [x] **Step 4: Inspect diff before handoff.**

~~~bash
cd /home/kaito/project/aurora-client
rtk git diff --check
rtk git status --short
rtk git diff --stat
~~~

Confirm only planned FE files and lock/config changes are present. Do not commit automatically.

---

### Task 10: Prove the real FE → BFF → Notification → FCM flow

**Runtime prerequisites:**

~~~text
RabbitMQ healthy on localhost:5672
Notification PostgreSQL migrated and healthy
Redis healthy on localhost:6379
Notification running on http://localhost:6001
Notification /health and /ready return Healthy
Staff BFF running with Redis__Host=localhost:6379
Staff BFF has matching Grpc__Notification__ServiceApiKey
API Gateway routes /api/v1/notifications to Staff BFF
Firebase Admin JSON exists only in backend ignored path
FE .env.local has public Web config and VAPID key
~~~

- [ ] **Step 1: Start backend processes without exposing secrets.**

Keep Notification alive. In another terminal:

~~~bash
cd /home/kaito/project/aurora-server
export Redis__Host='localhost:6379'
export Grpc__Notification__ServiceApiKey='local-notification-key'
dotnet run --project src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj
~~~

Use the API Gateway URL for FE; do not point browser code directly at Notification :6001.

- [ ] **Step 2: Start FE and complete real authentication.**

~~~bash
cd /home/kaito/project/aurora-client
pnpm dev
~~~

Open the dashboard, complete BFF/Cognito login, and verify GET /api/v1/auth/me returns isAuthenticated true with notifications:access. If it returns 401/403, stop popup testing and fix auth/permission provisioning; do not add a client bypass.

- [ ] **Step 3: Verify browser registration.**

In DevTools verify:

~~~text
Application => /firebase-messaging-sw.js is activated
browser permission => granted
POST /api/v1/notifications/devices => 200 with id/platform/isActive
request body contains token/platform/appVersion only
no console output contains token, Admin JSON or private key
~~~

- [ ] **Step 4: Subscribe to a real shipment.**

Open shipment detail and activate notifications. Verify:

~~~text
POST /api/v1/notifications/subscriptions/shipments/<shipmentId> => 204
~~~

The shipment must belong to the authenticated tenant through existing backend authorization.

- [ ] **Step 5: Produce a real event through its owning service.**

Use the authenticated Shipment Workflow UI/API to create a shipment or change its status. Do not insert a Notification row directly and do not hand-publish an event that bypasses the producer outbox. Verify the producer outbox publishes through RabbitMQ and Notification logs bounded event/attempt identifiers without tokens.

- [ ] **Step 6: Verify foreground popup and persisted history.**

Keep FE focused and produce an event:

~~~text
one Sonner popup appears with title/body
Open navigates only to /shipments/<guid> or /notifications
GET /api/v1/notifications contains the persisted row
GET /api/v1/notifications/unread-count increments
mark read calls PATCH and list/count update
~~~

- [ ] **Step 7: Verify background popup and click.**

Move the tab to background, produce a second event, and verify exactly one OS/browser notification. Click it and verify same-origin page focus/open. Invalid action paths must fall back to /notifications and never open an external URL.

- [ ] **Step 8: Verify failure and lifecycle paths.**

~~~text
permission denied => no device registration
logout => device DELETE is attempted and local device id is cleared
reopen with permission granted => token re-registers without duplicate in-flight calls
invalid/expired FCM token => backend deactivates it; later token can register
duplicate backend event => one history row and one delivery attempt
no subscriber => no popup; backend records NoRecipient safely
~~~

- [ ] **Step 9: Record evidence in the plan and relevant docs.**

Record exact commands and observed HTTP statuses. If API Gateway, Cognito, browser permission, Firebase project or producer event is unavailable, mark that acceptance check blocked with its exact reason; do not claim popup completion.

---

## Definition of Done

- [x] Firebase Web dependency and public environment schema exist; Admin JSON is absent from FE.
- [x] Axios sends BFF cookie credentials and the real login path creates an authenticated session.
- [x] Notification service, Zod response validation, query keys, queries and mutations exist in canonical locations.
- [x] Browser permission is requested only after user action and token registration uses POST /api/v1/notifications/devices.
- [x] Token refresh/reopen re-registers without duplicate concurrent calls; logout/disable deactivates the device where possible.
- [x] Foreground onMessage renders one Sonner popup and invalidates notification queries.
- [x] Background service worker displays/clicks one notification and enforces the internal action-path allowlist.
- [x] Staff Notification Center and header bell use live BFF data, not staff notification mocks.
- [x] Shipment detail can subscribe the current user through the BFF.
- [x] Missing permission, 401, 403, validation error and unavailable Firebase states are handled without security bypass.
- [x] FE lint, typecheck, tests and build pass; secret/token scans pass after excluding test marker construction.
- [ ] A real event produces a real browser popup through producer outbox → RabbitMQ → Notification → Firebase → FE, with persisted history/read-state verification.

## Explicit Non-Goals

- Không thêm Firebase Admin SDK vào FE.
- Không expose Notification gRPC hoặc service API key tới browser.
- Không tạo tenant-wide broadcast hoặc client-controlled recipient selection.
- Không biến customer portal mock notification thành Staff BFF notification khi backend chưa có customer contract.
- Không tự động commit, push, merge hoặc sửa repository backend trong lúc triển khai FE plan.
