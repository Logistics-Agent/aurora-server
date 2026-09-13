# Class Diagrams

Thư mục này chứa mã nguồn PlantUML (`.puml`) và hình ảnh (`.png`) sơ đồ lớp miền nghiệp vụ (Domain Model & Class Diagram) của nền tảng Aurora.

## Danh sách sơ đồ
- **`class-iam.puml` / `class-iam.png`**: Cấu trúc lớp IAM (Tenant, User, BaseRole, Permission, Group, Direct Capabilities).
- **`class-shipment.puml` / `class-shipment.png`**: Cấu trúc Aggregate Root Shipment, Cargo, Location, Milestone và Status.
- **`class-route-billing.puml` / `class-route-billing.png`**: Cấu trúc liên kết Route, Stop, Vehicle, Invoice, LineItem và Settlement.

## Cách tạo / render lại ảnh
```bash
python generate_class.py
```
