# Tài liệu Danh sách Events - IAM & Tenant Service

Dịch vụ `IamTenant` sử dụng **MassTransit** và **RabbitMQ** để phát hành (publish) các sự kiện tích hợp (Integration Events) ra Message Broker. Các dịch vụ khác (đặc biệt là **Email Agent**) có thể đăng ký (subscribe) các sự kiện này để thực hiện các tác vụ bất đồng bộ liên quan.

---

## 1. Danh sách Integration Events

### 1.1. Sự kiện: `TenantAdminCreatedEvent`
* **Exchange Name (RabbitMQ):** `tenant_admin_created_event`
* **Mô tả:** Được kích hoạt ngay sau khi System Admin tạo thành công một Tenant (doanh nghiệp) mới trên hệ thống, đồng thời khởi tạo tài khoản Admin/Director cho Tenant đó.
* **Mục đích sử dụng:** Thường dùng để gửi email chào mừng doanh nghiệp mới, gửi thông tin tài khoản đăng nhập ban đầu và mật khẩu tạm thời cho Director.
* **Payload cấu trúc:**
  ```json
  {
    "tenantId": "Guid - ID định danh của Tenant",
    "tenantName": "string - Tên doanh nghiệp",
    "userId": "Guid - ID định danh của tài khoản Admin vừa tạo",
    "email": "string - Email nhận tài khoản của Admin"
  }
  ```

---

### 1.2. Sự kiện: `TenantStaffCreatedEvent`
* **Exchange Name (RabbitMQ):** `tenant_staff_created_event`
* **Mô tả:** Được kích hoạt khi Tenant Admin hoặc Director mời thành công một nhân viên mới vào tổ chức (`InviteUser`).
* **Mục đích sử dụng:** Dùng để gửi email thông báo kèm link hoàn tất lời mời đăng ký tài khoản (kèm mã kích hoạt/confirmation code) cho nhân viên.
* **Payload cấu trúc:**
  ```json
  {
    "tenantId": "Guid - ID định danh của Tenant sở tại",
    "userId": "Guid - ID định danh của tài khoản nhân viên",
    "email": "string - Email nhận lời mời của nhân viên",
    "firstName": "string - Tên nhân viên",
    "lastName": "string - Họ nhân viên"
  }
  ```

---

### 1.3. Sự kiện: `TenantStaffPasswordResetEvent`
* **Exchange Name (RabbitMQ):** `tenant_staff_password_reset_event`
* **Mô tả:** Được kích hoạt khi người dùng yêu cầu khôi phục mật khẩu hoặc Tenant Admin chủ động đặt lại mật khẩu cho nhân viên.
* **Mục đích sử dụng:** Email Agent lắng nghe để gửi mã OTP / Token khôi phục mật khẩu qua Email cho người dùng.
* **Payload cấu trúc:**
  ```json
  {
    "tenantId": "Guid - ID định danh của Tenant",
    "userId": "Guid - ID định danh của người dùng cần reset",
    "email": "string - Email nhận mã khôi phục"
  }
  ```

---

### 1.4. Sự kiện: `RolePermissionsChangedEvent`
* **Exchange Name (RabbitMQ):** `role_permissions_changed_event`
* **Mô tả:** Được kích hoạt khi vai trò (Role) bị chỉnh sửa cấu hình các quyền hạn (Permissions) đi kèm.
* **Mục đích sử dụng:** BFF Gateway hoặc các dịch vụ caching (Redis) lắng nghe sự kiện này để thực hiện thu hồi/làm mới bộ nhớ đệm (Cache Eviction) của những người dùng sở hữu vai trò đó, đảm bảo quyền mới được cập nhật tức thì.
* **Payload cấu trúc:**
  ```json
  {
    "roleId": "Guid - ID của vai trò bị thay đổi quyền"
  }
  ```
