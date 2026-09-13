# Use Case Diagrams

Thư mục này chứa mã nguồn PlantUML (`.puml`) và hình ảnh (`.png`) sơ đồ Use Case của nền tảng Aurora.

## Danh sách sơ đồ
- **`use-case.puml` / `use-case.png`**: Tổng quan toàn bộ hệ thống Use Case (Platform Admin, Tenant Admin, Logistics Manager, Operational Staff, Driver, Customer).
- **`use-case-iam.puml` / `use-case-iam.png`**: Phân hệ IAM, Tenancy, Phân quyền & Quản lý User/Group.
- **`use-case-operations.puml` / `use-case-operations.png`**: Phân hệ Vận hành logistics (Tạo shipment, Lập kế hoạch Route, Tối ưu hóa lộ trình, Theo dõi GPS, Billing).
- **`use-case-platform.puml` / `use-case-platform.png`**: Phân hệ Quản trị nền tảng SaaS (Tenant onboarding, Quản lý tài nguyên, Audit log, OCR & AI agent).

## Cách tạo / render lại ảnh
```bash
python generate_use_case.py
```
