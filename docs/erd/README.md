# Entity Relationship Diagrams (ERD)

Thư mục này chứa mã nguồn PlantUML (`.puml`) và hình ảnh (`.png`) sơ đồ thực thể cơ sở dữ liệu của nền tảng Aurora.

## Danh sách sơ đồ
- **`er-shipment.puml` / `er-shipment.png`**: Cơ sở dữ liệu Shipment Workflow (Shipment, CargoItem, ShipmentLocation, ShipmentDocument, Milestone, StatusHistory).
- **`er-billing.puml` / `er-billing.png`**: Cơ sở dữ liệu Billing & Settlement (Invoice, InvoiceLineItem, PaymentRecord, SettlementBatch).
- **`er-iam.puml` / `er-iam.png`**: Cơ sở dữ liệu IAM & Tenancy (Tenant, User, Permission, UserPermission, Group).

## Cách tạo / render lại ảnh
Chạy lệnh sau bằng Python (tự động sử dụng PlantUML JAR cục bộ hoặc fallback qua PlantUML server):
```bash
python generate_erd.py
```
