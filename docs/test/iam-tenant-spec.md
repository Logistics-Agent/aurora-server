# Tài liệu Yêu cầu Chức năng, Phi chức năng & Test Cases - IAM & Auth Service

Tài liệu này cung cấp kết quả khảo sát, đặc tả yêu cầu chức năng (Functional Requirements), yêu cầu phi chức năng (Non-functional Requirements) và danh sách kịch bản kiểm thử (Test Cases) cho cụm dịch vụ **`IamService`** (trong `iam_tenant.proto`) và **`AuthService`** (trong `auth.proto`) thuộc hệ thống Aurora.

---

## 1. Khảo sát & Mô tả Nghiệp vụ (Overview)
Hệ thống quản lý định danh và phân quyền trên nhánh `feat/iam-tennant` được chia làm hai dịch vụ chính:
1. **`IamService` (`iam_tenant.proto`):** Chịu trách nhiệm quản lý cấu trúc doanh nghiệp (Tenant), tài khoản người dùng/nhân viên (User/Staff), quản lý vai trò tùy chỉnh (Role) và ánh xạ quyền hạn (Permission).
2. **`AuthService` (`auth.proto`):** Chịu trách nhiệm thực hiện các luồng đăng nhập 3 trường (`tenantCode`, `email`, `password`), xác minh token (BFF Gateway gọi để kiểm tra phiên làm việc), làm mới token, khôi phục mật khẩu và hoàn tất quy trình mời nhân sự tham gia hệ thống.

---

## 2. Yêu cầu Chức năng (Functional Requirements - FR)

### FR-01: Quản lý Tenant (IamService)
* **FR-01.1 (Tạo Tenant):** Cho phép System Admin tạo Tenant mới với các trường: Tên Tenant, Mã định danh (`tenantCode`), Email Admin, Tên Admin, Gói dịch vụ (`planType`), Tên miền công ty (`companyDomain`).
* **FR-01.2 (Xem Tenant):** Tra cứu thông tin Tenant bằng ID hoặc `tenantCode`.
* **FR-01.3 (Cập nhật trạng thái Tenant):** Kích hoạt hoặc tạm ngưng hoạt động của Tenant (`ACTIVE` / `SUSPENDED`).

### FR-02: Quản lý Người dùng / Nhân sự (IamService)
* **FR-02.1 (Mời Người dùng):** Tenant Admin gửi lời mời qua email kèm theo vai trò ban đầu (`InviteUser`). Hệ thống tạo tài khoản tạm thời.
* **FR-02.2 (Xem thông tin Người dùng):** Xem chi tiết thông tin và danh sách Roles được gán.
* **FR-02.3 (Danh sách Người dùng):** Hỗ trợ phân trang và bộ lọc theo vai trò (`roleId`) và trạng thái (`status`: `ACTIVE`, `INACTIVE`, `BLOCKED`).
* **FR-02.4 (Gán Vai trò):** Cho phép cập nhật lại danh sách vai trò (`AssignRoles`) cho người dùng.
* **FR-02.5 (Tạm ngưng Người dùng):** Chuyển trạng thái người dùng sang tạm ngưng hoặc khóa (`SuspendUser`).

### FR-03: Quản lý Vai trò & Quyền hạn (IamService)
* **FR-03.1 (Vai trò tùy chỉnh):** Cho phép tạo, sửa, xóa, lấy danh sách các vai trò chuyên môn (`Role`) của từng doanh nghiệp.
* **FR-03.2 (Gán Quyền cho Vai trò):** Ánh xạ danh sách các mã quyền hạn (`PermissionInfo` gồm `code`, `module`, `resource`, `action`) vào từng Vai trò (`AssignPermissionsToRole`).
* **FR-03.3 (Lấy Quyền hạn người dùng):** API trả về danh sách chi tiết toàn bộ các quyền cụ thể của người dùng để Gateway hoặc Client thực hiện kiểm tra quyền truy cập.

### FR-04: Xác thực & Đăng nhập (AuthService)
* **FR-04.1 (Xác định người dùng):** API kiểm tra nhanh xem Email có tồn tại trên hệ thống và thuộc Tenant nào (`IdentifyUser`).
* **FR-04.2 (Đăng nhập):** Hỗ trợ đăng nhập với `tenantCode`, `email`, `password`. Trả về `accessToken`, `refreshToken`, và danh sách `permissions` đi kèm.
* **FR-04.3 (Xác thực Token):** BFF Gateway gọi API `ValidateToken` để giải mã và kiểm tra tính hợp lệ của Token trước khi định tuyến request vào các microservices nội bộ.
* **FR-04.4 (Làm mới & Đăng xuất):** Hỗ trợ Token Refresh (`RefreshToken`) và thu hồi phiên đăng nhập (`Logout`).
* **FR-04.5 (Quên mật khẩu & Hoàn tất lời mời):** Khôi phục mật khẩu và hoàn tất việc thiết lập mật khẩu lần đầu sau khi nhận email mời (`CompleteInvitation`).

---

## 3. Yêu cầu Phi chức năng (Non-functional Requirements - NFR)
* **NFR-01 (Bảo mật):**
  * Mã hóa mật khẩu người dùng trước khi lưu trữ trong Database (sử dụng BCrypt hoặc Argon2).
  * Bảo vệ các đầu API gRPC nội bộ thông qua Interceptor kiểm tra Token.
* **NFR-02 (Cách ly dữ liệu):** Dữ liệu của các Tenant phải được phân tách hoàn toàn ở tầng Logic và Database (Multi-tenancy). Không được để rò rỉ dữ liệu giữa các tenantCode khác nhau.
* **NFR-03 (Hiệu năng):**
  * Thời gian phản hồi gRPC đối với các API kiểm tra Token (`ValidateToken`) và phân quyền (`GetUserPermissions`) phải nhỏ hơn **50ms** để không ảnh hưởng đến trải nghiệm người dùng qua API Gateway.
  * Hỗ trợ lưu bộ nhớ đệm (Caching) thông tin phân quyền của User qua Redis để giảm tải cho Database chính.

---

## 4. Kịch bản kiểm thử (Test Cases - TC)
Dưới đây là **6 kịch bản kiểm thử** đại diện cho các tính năng cốt lõi trên nhánh phát triển này:

### TC-01: Khởi tạo Tenant mới thành công (CreateTenant - Happy Path)
* **Mô tả:** System Admin tạo doanh nghiệp thành công và cấu hình dữ liệu ban đầu.
* **Các bước thực hiện:**
  1. Gọi `CreateTenant` với:
     * `name`: "Aurora Logistic Group"
     * `tenantCode`: "auroralog"
     * `adminEmail`: "admin@auroralog.com"
     * `planType`: `ENTERPRISE`
  2. Kiểm tra xem Tenant được lưu trữ thành công với trạng thái `ACTIVE`.
* **Kết quả mong đợi:**
  * Phản hồi trả về object `TenantResponse` có ID hợp lệ, trạng thái là `ACTIVE`.
  * Không có lỗi trùng lặp xảy ra.

### TC-02: Đăng nhập thành công với thông tin Tenant định danh (Login - Happy Path)
* **Mô tả:** Đăng nhập thành công bằng cơ chế 3 trường thông tin.
* **Các bước thực hiện:**
  1. Gửi request `Login` chứa:
     * `tenantCode`: "auroralog"
     * `email`: "admin@auroralog.com"
     * `password`: "Mật khẩu đúng"
* **Kết quả mong đợi:**
  * Trả về kết quả thành công.
  * Response chứa đầy đủ `accessToken`, `refreshToken`, `userId`, `tenantId` và danh sách các quyền hạn (`permissions`) của user.

### TC-03: Đăng nhập thất bại khi thông tin không chính xác (Login - Edge Case)
* **Mô tả:** Từ chối yêu cầu đăng nhập khi sai mã Tenant hoặc sai tài khoản/mật khẩu.
* **Các bước thực hiện:**
  1. Gửi request `Login` với mã `tenantCode` sai (ví dụ: "auroralog_error").
  2. Gửi request `Login` với email hoặc password không chính xác.
* **Kết quả mong đợi:**
  * Hệ thống từ chối xác thực.
  * Trả về mã lỗi gRPC `UNAUTHENTICATED` hoặc `INVALID_ARGUMENT`.

### TC-04: Đăng nhập thất bại do Tenant bị tạm ngưng (Login & Tenant Status - Edge Case)
* **Mô tả:** Nhân viên không thể đăng nhập nếu Tenant sở tại đang ở trạng thái bị tạm ngưng (`SUSPENDED`).
* **Các bước thực hiện:**
  1. Chuyển trạng thái Tenant "auroralog" sang `SUSPENDED` bằng API `UpdateTenantStatus`.
  2. Gửi request `Login` bằng tài khoản hợp lệ của Tenant này.
* **Kết quả mong đợi:**
  * Đăng nhập thất bại.
  * Trả về mã lỗi gRPC `PERMISSION_DENIED` cùng thông báo: Tenant đã bị khóa.

### TC-05: Khởi tạo vai trò tùy chỉnh và gán quyền thành công (CreateCustomRole & AssignPermissionsToRole)
* **Mô tả:** Tenant Admin thiết lập vai trò nghiệp vụ riêng biệt và phân quyền cho vai trò đó.
* **Các bước thực hiện:**
  1. Gọi `CreateCustomRole` để tạo vai trò `DriverDispatcher`.
  2. Gọi `AssignPermissionsToRole` để gán danh sách quyền `["OP_ROUTE_CREATE", "OP_ROUTE_EDIT"]` cho vai trò vừa tạo.
  3. Kiểm tra thông tin cấu hình vai trò.
* **Kết quả mong đợi:**
  * Bước 1: Tạo vai trò thành công, trả về Role ID.
  * Bước 2 & 3: Cập nhật thành công, vai trò `DriverDispatcher` sở hữu chính xác các Permission ID tương ứng.

### TC-06: Xác thực Token hợp lệ thông qua BFF Gateway (ValidateToken - Happy Path)
* **Mô tả:** Gateway xác thực thành công Token hợp lệ từ Client.
* **Các bước thực hiện:**
  1. Lấy `accessToken` từ kết quả đăng nhập thành công ở **TC-02**.
  2. Gửi request `ValidateToken` chứa token này từ phía BFF Gateway.
* **Kết quả mong đợi:**
  * Phản hồi trả về `valid: true`.
  * Chứa đầy đủ thông tin định danh của người dùng (`userId`, `tenantId`, `roles`, `permissions`) để Gateway tiếp tục định tuyến.
