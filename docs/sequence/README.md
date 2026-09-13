# Sequence Diagrams

Thư mục này chứa mã nguồn PlantUML (`.puml`) và hình ảnh (`.png`) sơ đồ tuần tự (Sequence Diagram) của nền tảng Aurora.

## Danh sách sơ đồ
- **`sequence-login.puml` / `sequence-login.png`**: Quy trình xác thực người dùng (Login), tương tác AWS Cognito, gRPC IAM và Redis Permission cache.
- **`sequence-shipment.puml` / `sequence-shipment.png`**: Quy trình tạo và submit Shipment qua BFF, gRPC, Transactional Outbox và RabbitMQ.
- **`sequence-route.puml` / `sequence-route.png`**: Quy trình lập kế hoạch và tối ưu lộ trình (Route Planning & Optimization via VRP Engine).

## Cách tạo / render lại ảnh
```bash
python generate_sequence.py
```
