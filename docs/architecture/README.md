# System Architecture Diagrams

Thư mục này chứa mã nguồn PlantUML (`.puml`) và hình ảnh (`.png`) sơ đồ kiến trúc hệ thống và luồng sự kiện của nền tảng Aurora.

## Danh sách sơ đồ
- **`architecture.puml` / `architecture.png`**: Tổng quan kiến trúc hệ thống microservices của nền tảng Aurora.
- **`architecture-platform-context.puml` / `architecture-platform-context.png`**: Bối cảnh nền tảng SaaS đa người thuê (Multi-tenancy).
- **`architecture-runtime.puml` / `architecture-runtime.png`**: Mô hình triển khai runtime các dịch vụ .NET, NestJS, Python AI agents, Databases & Message Brokers.
- **`deployment-week5.puml` / `deployment-week5.png`**: Sơ đồ triển khai hạ tầng đám mây / Docker Compose môi trường production & local.
- **`event-driven.puml` / `event-driven.png`**: Kiến trúc Event-Driven với Transactional Outbox Pattern và RabbitMQ Broker.

## Cách tạo / render lại ảnh
```bash
python generate_architecture.py
```
