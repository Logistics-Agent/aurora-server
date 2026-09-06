# 📘 Hướng Dẫn Tích Hợp Authentication Dành Cho Frontend (FE)

> **Dành cho:** Frontend Developer (React, Next.js, Vue, Angular, Mobile Web, v.v.)  
> **Mục tiêu:** Hiểu rõ cách thức hoạt động của hệ thống Xác thực (Auth), Đăng nhập, Đăng xuất, Đổi mật khẩu, và Luồng mời người dùng (Tenant / Staff) mà không cần phải can thiệp sâu vào code backend.

---

## 📌 1. Nguyên Tắc Cốt Lõi (Cần Nhớ)

Hệ thống sử dụng mô hình **BFF (Backend-For-Frontend) với Cookie HttpOnly (`.Aurora.Auth`)**:

1. **KHÔNG lưu Token trong `localStorage` hay `sessionStorage`**:
   - Toàn bộ token (Access Token, Refresh Token) được mã hóa và lưu trữ bên trong **Cookie HttpOnly** do trình duyệt tự quản lý.
   - Giúp ứng dụng **miễn nhiễm 100% với tấn công đánh cắp token qua XSS**.
2. **Luôn bật gửi Cookie trong mọi request API**:
   - `fetch`: Cần cấu hình `credentials: 'include'`.
   - `axios`: Cần cấu hình `withCredentials: true`.
3. **Cách lấy thông tin User**:
   - FE không decode JWT. Thay vào đó, gọi endpoint `GET /api/v1/auth/me` để lấy thông tin người dùng hiện tại (Email, Role, Direct Permissions, TenantId, v.v.).

---

## 🔐 2. Chi Tiết Các Luồng Xác Thực (Auth Flows)

```
                       ┌──────────────────────────────┐
                       │      Frontend (SPA / Web)     │
                       └──────────────┬───────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              ▼                       ▼                       ▼
     【 1. LOGIN FLOW 】      【 2. LOGOUT FLOW 】     【 3. GET USER ME 】
    Chuyển hướng trình     Chuyển hướng hoặc gọi   Gọi GET /api/v1/auth/me
    duyệt sang Cognito     API xóa Cookie session   lấy Role & Permissions
```

---

### Flow 1: Đăng Nhập (Login via Cognito Hosted UI)

FE không cần tự vẽ form nhập tài khoản/mật khẩu (trừ khi dùng flow riêng), mà chỉ cần chuyển hướng trình duyệt sang endpoint Login của BFF.

#### Cách thực hiện:
Khi người dùng bấm nút **"Đăng nhập"**, FE chuyển hướng URL của trình duyệt:
```javascript
const returnUrl = encodeURIComponent(window.location.origin + '/dashboard');
window.location.href = `https://api.humanak.cyou/api/v1/auth/login?returnUrl=${returnUrl}`;
```

#### Tiến trình xử lý ngầm:
1. Trình duyệt gọi tới `GET /api/v1/auth/login`.
2. BFF chuyển hướng tiếp sang giao diện đăng nhập bảo mật **AWS Cognito Hosted UI**.
3. Người dùng nhập Email và Mật khẩu trên Cognito.
4. Cognito xác thực xong -> tự động chuyển hướng về `/api/v1/auth/callback`.
5. BFF lưu cookie `.Aurora.Auth` vào trình duyệt và redirect người dùng về lại `returnUrl` (`/dashboard`).
6. Trang `/dashboard` của FE load lên, gọi `GET /api/v1/auth/me` để lấy quyền và hiển thị giao diện.

---

### Flow 2: Đăng Xuất (Logout)

Khi người dùng bấm nút **"Đăng xuất"**, FE chuyển hướng trình duyệt hoặc gọi API:

```javascript
const returnUrl = encodeURIComponent(window.location.origin + '/login');
window.location.href = `https://api.humanak.cyou/api/v1/auth/logout?returnUrl=${returnUrl}`;
```

#### Tiến trình xử lý:
1. BFF xóa cookie session `.Aurora.Auth` trên trình duyệt.
2. BFF tiếp tục chuyển hướng sang Cognito Logout để hủy phiên SSO của Cognito.
3. Cognito redirect người dùng trở lại trang `returnUrl` (trang chủ hoặc trang thông báo đăng xuất).

---

### Flow 3: Lấy Thông Tin Người Dùng Hiện Tại (`/me`)

Dùng để khởi tạo trạng thái đăng nhập (Auth Context) khi ứng dụng vừa tải hoặc F5 lại trang.

* **Endpoint:** `GET /api/v1/auth/me`
* **Request Header:** Không cần truyền `Authorization: Bearer ...` (cookie tự động đính kèm nhờ `withCredentials: true`).

#### Response mẫu khi đã đăng nhập (`200 OK`):
```json
{
  "email": "admin@acme.com",
  "emailDomain": "acme.com",
  "cognitoSub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "tenantId": "c9a646d3-9c61-4cd9-bc22-38e55e8c1b82",
  "role": "TENANT_ADMIN",
  "permissions": [
    "user:view",
    "user:create",
    "user:update",
    "staff:manage",
    "tenant:settings:view"
  ],
  "name": "Acme Admin",
  "isAuthenticated": true
}
```

#### Response khi chưa đăng nhập (`401 Unauthorized`):
* FE bắt mã lỗi `401` và chuyển hướng người dùng sang trang Login.

---

### Flow 4: Quên Mật Khẩu / Đổi Mật Khẩu (Forgot / Reset Password)

1. **Trên giao diện Cognito Hosted UI**:
   - Ngay dưới ô đăng nhập có sẵn đường link **"Forgot your password?"**.
   - Người dùng click vào -> Cognito tự gửi mã xác thực (OTP) về email -> Người dùng nhập OTP và mật khẩu mới trực tiếp trên Cognito.
2. **Nếu FE tự dựng màn hình Forgot Password riêng**:
   - Bước 1: Gọi `POST /api/v1/auth/forgot-password` với `{ "email": "user@domain.com" }` để nhận mã xác nhận qua email.
   - Bước 2: Gọi `POST /api/v1/auth/confirm-forgot-password` với `{ "email": "...", "confirmationCode": "123456", "newPassword": "..." }`.

---

## 👥 3. Luồng Tạo Người Dùng Mới (Tenant & Staff)

---

### Case A: System Admin tạo Tenant mới (Kèm tài khoản Tenant Admin)

```
[System Admin Portal] ──> Gọi POST /api/v1/system/tenants
                                   │
                                   ▼
             Hệ thống tạo Tenant + Admin User Pool (Cognito)
             Trạng thái User: FORCE_CHANGE_PASSWORD (Invited)
                                   │
                                   ▼
          MailService gửi Email Lời mời đến Tenant Admin
                                   │
                                   ▼
       Tenant Admin mở Email ──> Nhận thông tin đăng nhập tạm
                                   │
                                   ▼
        Truy cập trang Đăng nhập ──> Nhập Mật khẩu tạm thời
                                   │
                                   ▼
     Hệ thống yêu cầu đổi mật khẩu ──> Nhập Mật khẩu Mới chính thức
                                   │
                                   ▼
               Tài khoản kích hoạt thành công (CONFIRMED)
                    Đăng nhập vào Tenant Admin Portal
```

#### Các bước chi tiết:
1. **System Admin tạo Tenant:** Điền Tên công ty, Company Domain (`acme.com`), Email Admin (`admin@acme.com`).
2. **Hệ thống tạo tài khoản:** Backend tạo một User Pool riêng cho Tenant và tạo user với mật khẩu tạm thời.
3. **Email thông báo:** Email được gửi đến `admin@acme.com` kèm:
   - Tên công ty / Mã Tenant.
   - Đường dẫn đăng nhập vào trang quản trị.
   - Mật khẩu tạm thời (Temporary Password).
4. **Tenant Admin kích hoạt:**
   - Đăng nhập lần đầu bằng mật khẩu tạm.
   - Hệ thống hiển thị màn hình yêu cầu đặt lại mật khẩu mới.
   - Sau khi đặt mật khẩu mới thành công, tài khoản chuyển sang trạng thái `Active`, được cấp Cookie và chuyển vào Dashboard.

---

### Case B: Tenant Admin mời Nhân viên (Staff) mới vào công ty

```
[Tenant Admin Portal] ──> Gọi POST /api/v1/admin/staffs
                                   │
                                   ▼
              Backend kiểm tra domain email (@acme.com)
            Gán Direct Permissions vào DB + Tạo Cognito User
                                   │
                                   ▼
            MailService gửi Email Lời mời kích hoạt đến Staff
                                   │
                                   ▼
         Nhân viên mở Email ──> Click Link kích hoạt tài khoản
                                   │
                                   ▼
       Đăng nhập lần đầu với Mật khẩu tạm ──> Đặt Mật khẩu mới
                                   │
                                   ▼
             Tài khoản kích hoạt (CONFIRMED) ──> Đăng nhập
                     Truy cập Staff Portal theo Quyền
```

#### Các bước chi tiết:
1. **Tenant Admin tạo Staff:** Điền Email nhân viên (phải thuộc domain của công ty, ví dụ `staff1@acme.com`), Họ tên, Role (`STAFF`, `MANAGER`), và tích chọn các quyền hạn (Permissions).
2. **Hệ thống gửi Email:** Backend lưu user vào DB với trạng thái `Invited` và bắn event để `MailService` gửi email mời gia nhập công ty.
3. **Nhân viên kích hoạt:**
   - Mở email và bấm vào liên kết kích hoạt.
   - Thực hiện đổi mật khẩu tạm thời sang mật khẩu cá nhân (qua màn hình Đăng nhập hoặc gọi API `POST /api/v1/auth/complete-invitation`).
   - Đăng nhập thành công và sử dụng các chức năng được phân quyền.

---

## 💻 4. Code Mẫu Dành Cho Frontend (React / TypeScript)

### Cấu hình Axios Client (`apiClient.ts`)

```typescript
import axios from 'axios';

export const apiClient = axios.create({
  baseURL: 'https://api.humanak.cyou',
  // QUAN TRỌNG: Bắt buộc bật để tự động gửi HttpOnly cookie .Aurora.Auth
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor tự động xử lý khi hết hạn phiên đăng nhập (401)
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Lưu lại URL hiện tại để login xong quay lại
      const returnUrl = encodeURIComponent(window.location.href);
      window.location.href = `https://api.humanak.cyou/api/v1/auth/login?returnUrl=${returnUrl}`;
    }
    return Promise.reject(error);
  }
);
```

---

### React Auth Context & Hook (`AuthContext.tsx`)

```tsx
import React, { createContext, useContext, useEffect, useState } from 'react';
import { apiClient } from './apiClient';

interface CurrentUser {
  email: string;
  userId: string;
  tenantId: string;
  role: string;
  permissions: string[];
  name?: string;
  isAuthenticated: boolean;
}

interface AuthContextType {
  user: CurrentUser | null;
  loading: boolean;
  login: (redirectPath?: string) => void;
  logout: (redirectPath?: string) => void;
  hasPermission: (permissionCode: string) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);

  // Khởi tạo: kiểm tra phiên đăng nhập bằng cách gọi /me
  useEffect(() => {
    apiClient
      .get<CurrentUser>('/api/v1/auth/me')
      .then((res) => setUser(res.data))
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  const login = (redirectPath = window.location.pathname) => {
    const returnUrl = encodeURIComponent(window.location.origin + redirectPath);
    window.location.href = `https://api.humanak.cyou/api/v1/auth/login?returnUrl=${returnUrl}`;
  };

  const logout = (redirectPath = '/') => {
    const returnUrl = encodeURIComponent(window.location.origin + redirectPath);
    window.location.href = `https://api.humanak.cyou/api/v1/auth/logout?returnUrl=${returnUrl}`;
  };

  const hasPermission = (permissionCode: string): boolean => {
    if (!user || !user.permissions) return false;
    return user.permissions.includes(permissionCode);
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
};
```

---

### Sử Dụng Trong Component

```tsx
import React from 'react';
import { useAuth } from './AuthContext';

export const DashboardHeader: React.FC = () => {
  const { user, loading, login, logout, hasPermission } = useAuth();

  if (loading) return <div>Đang tải...</div>;

  if (!user?.isAuthenticated) {
    return (
      <button onClick={() => login('/dashboard')}>
        🔑 Đăng nhập qua Cognito
      </button>
    );
  }

  return (
    <div>
      <span>Xin chào, {user.name || user.email} ({user.role})</span>

      {/* Kiểm tra quyền hiển thị chức năng */}
      {hasPermission('staff:manage') && (
        <button onClick={() => window.location.href = '/staff-management'}>
          Quản lý nhân viên
        </button>
      )}

      <button onClick={() => logout('/')}>🚪 Đăng xuất</button>
    </div>
  );
};
```

---

## 📋 5. Bảng Tổng Hợp Endpoint Dành Cho FE

| Phương thức | Endpoint | Ý nghĩa & Hành vi của FE |
| :--- | :--- | :--- |
| `GET` | `/api/v1/auth/login?returnUrl={url}` | Chuyển hướng `window.location.href` để đăng nhập qua Cognito |
| `GET` / `POST` | `/api/v1/auth/logout?returnUrl={url}` | Chuyển hướng để xóa cookie session & Cognito SSO |
| `GET` | `/api/v1/auth/me` | Lấy User Info, Role & danh sách Direct Permissions |
| `POST` | `/api/v1/auth/complete-invitation` | Dành cho user mới được mời nhập mật khẩu lần đầu (nếu làm form FE) |
| `POST` | `/api/v1/admin/staffs` | Tenant Admin tạo nhân viên mới (kèm gán permissions) |
| `POST` | `/api/v1/system/tenants` | System Admin tạo Tenant mới |
