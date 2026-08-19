# TÀI LIỆU KỸ THUẬT VÀ NGHIỆP VỤ REALTIME HUB SERVICE [CORE]

> **Phụ trách (Owner):** Đào Huỳnh  
> **Công nghệ:** NestJS (TypeScript), WebSockets (Socket.io), Redis Pub/Sub Adapter, Offline Buffer (Redis Stream), RabbitMQ Consumer Bridge  
> **Cổng kết nối WebSocket:** `5005`  
> **Cơ chế Mở rộng Ngang:** Stateless với `@socket.io/redis-adapter`  

---

## 1. TỔNG QUAN VÀ MỤC TIÊU PHÂN HỆ

Dịch vụ **Realtime Hub Service** là phân hệ đẩy dữ liệu thời gian thực (Real-time Downstream Push) trong hệ thống SaaS Logistics Aurora, chịu trách nhiệm:

1. **WebSocket Gateway:** Phục vụ hàng nghìn kết nối đồng thời từ Web Dashboard / Mobile App qua Socket.io.
2. **Stateless Horizontal Scaling:** Tích hợp `@socket.io/redis-adapter` với Redis Pub/Sub, cho phép scale out nhiều Container Replicas.
3. **Multi-Tenant Room Isolation:**
   - Room Tenant: `tenant:{tenant_id}`
   - Room User: `user:{tenant_id}:{user_id}`
   - Room Chuyến xe: `shipment:{tenant_id}:{shipment_id}`
4. **Offline Buffer & ACK Mechanism (TASK-005):** Gửi tin nhắn kèm `msgId`. Nếu Client không ACK trong 5 giây ➔ tự động lưu tin nhắn vào Redis Stream `stream:offline_msg:{tenant}:{user}`. Khi Client reconnect ➔ tự động xả (flush) buffer theo đúng thứ tự.
5. **Ping/Pong Heartbeat:** Client gửi `ping` mỗi 25-30 giây để giữ kết nối sống qua Load Balancer/Proxy.
6. **MQ Consumer Bridge:** Lắng nghe Exchange `logistics_events` (`billing.#`, `shipment.#`, `negotiation.#`, `financial.#`) và đẩy sang WebSockets.
7. **Terminus K8s Health Check:** Cung cấp `/healthz/liveness` và `/healthz/readiness`.

---

## 2. NGUYÊN TẮC THIẾT KẾ VÀ KIẾN TRÚC (CLEAN ARCHITECTURE)

```text
src/nestjs/realtime-hub-service/
├── src/
│   ├── config/                      # Read & Validate env vars via class-validator
│   ├── common/
│   │   ├── guards/ws-jwt.guard.ts   # Xác thực JWT Token trên Handshake
│   │   └── adapters/redis-io.adapter.ts # Redis Adapter cho Scale-out Cluster
│   ├── gateway/
│   │   ├── events.gateway.ts        # Socket.io Gateway + ACK Handler + Ping/Pong Heartbeat
│   │   └── dto/realtime-payload.dto.ts
│   ├── messaging/
│   │   ├── mq-consumer.service.ts   # Lắng nghe RabbitMQ Events
│   │   └── offline-buffer.service.ts # ★ Redis Stream Offline Message Buffer & Flush
│   └── health/
│       └── health.controller.ts     # ★ Terminus K8s Probes (/healthz/liveness & readiness)
```

---

## 3. HƯỚNG DẪN KHỞI CHẠY VÀ KIỂM THỬ

```powershell
# Chạy Realtime Hub Service
cd src/nestjs/realtime-hub-service
npm run start:dev

# Health Check Probes
curl http://localhost:5005/healthz/liveness
curl http://localhost:5005/healthz/readiness
```
