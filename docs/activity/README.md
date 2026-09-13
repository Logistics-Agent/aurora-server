# Activity Diagrams

Thư mục này chứa mã nguồn PlantUML (`.puml`) và hình ảnh (`.png`) sơ đồ hoạt động (Activity Diagram) của nền tảng Aurora.

## Danh sách sơ đồ
- **`activity-login.puml` / `activity-login.png`**: Quy trình xác thực người dùng, giải quyết Tenant, kiểm tra trạng thái tài khoản và nạp Direct Capabilities.
- **`activity-shipment-workflow.puml` / `activity-shipment-workflow.png`**: Vòng đời đơn hàng từ lúc tạo Draft, trích xuất OCR, kiểm tra tuân thủ, phát hành Outbox Event tới lúc giao hàng & kích hoạt Billing.
- **`activity-route-planning.puml` / `activity-route-planning.png`**: Quy trình tính toán tối ưu hóa tuyến đường vận chuyển bằng VRP Solver Engine, phê duyệt và giao việc cho tài xế.

## Cách tạo / render lại ảnh
```bash
python generate_activity.py
```
