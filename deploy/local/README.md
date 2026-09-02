# Aurora Local Development Environment

Comprehensive Docker Compose development stack supporting isolated layer profiles, database automatic initialization, and conflict-free Notification runtime testing.

## Quick Start

1. **Configure Environment**:
   ```bash
   cp .env.example .env
   ```

2. **Start Complete Platform Stack** (.NET Notification default):
   ```bash
   docker compose --profile full up -d
   ```

3. **Start Core Services Only**:
   ```bash
   docker compose --profile infra --profile core --profile notification-dotnet up -d
   ```

4. **Start AI & Optimization Services Only**:
   ```bash
   docker compose --profile infra --profile ai --profile routing up -d
   ```

5. **Test Experimental Java Notification**:
   ```bash
   docker compose --profile infra --profile core --profile notification-java up -d
   ```

## Port Mapping Reference

| Service | Local Host Port | Container Port | Protocol / Purpose |
|---|:---:|:---:|---|
| **`postgres`** | 5432 | 5432 | PostgreSQL DB Server |
| **`redis`** | 6379 | 6379 | Local Redis Cache |
| **`rabbitmq`** | 5672, 15672 | 5672, 15672 | AMQP / Management UI |
| **`mailhog`** | 1025, 8025 | 1025, 8025 | SMTP Test / Web Inbox UI |
| **`api-gateway`** | 8080 | 8080 | YARP Gateway Entry |
| **`staff-bff`** | 7101 | 8080 | Staff BFF HTTP REST |
| **`admin-bff`** | 7102 | 8080 | Admin BFF HTTP REST |
| **`system-bff`** | 7103 | 8080 | System BFF HTTP REST |
| **`iam-tenant`** | 5001 | 5000 | IAM gRPC Service |
| **`shipment-workflow`** | 5002 | 5000 | Shipment gRPC Service |
| **`route-planning-agent`**| 5003 | 5000 | Route Planning gRPC Service |
| **`gps-tracking`** | 5004 | 5000 | GPS Telemetry gRPC Service |
| **`document-ocr`** | 5005 | 5000 | Document OCR gRPC Service |
| **`regulatory-compliance`**| 5006 | 5000 | Compliance gRPC Service |
| **`notification` (.NET)** | 5007 | 5000 | Notification gRPC Service |
| **`notification` (Java)** | 8087 | 8080 | Notification Spring REST |
| **`mail-service`** | 5008 | 5003 | Mail Management Service |
| **`stalwart`** | 1026, 8085 | 25, 8080 | Stalwart SMTP / Admin UI |
| **`ai-governance`** | 8081 | 8080 | AI Governance Spring REST |
| **`devops-agent`** | 8082 | 8080 | DevOps Agent Spring REST |
| **`audit-service`** | 8089 | 8080 | Audit Service Spring REST |
| **`billing-service`** | 5014 | 5004 | Billing NestJS gRPC |
| **`financial-service`** | 5013 | 5003 | Financial NestJS gRPC |
| **`realtime-hub-service`**| 5018, 8088 | 5008, 8080 | Realtime gRPC & WebSocket |
| **`customer-assistant`** | 5017 | 5007 | Customer Assistant gRPC |
| **`negotiation-agent`** | 5016 | 5006 | Negotiation Agent gRPC |
| **`osrm`** | 5000 | 5000 | OSRM Routed Engine |
| **`vroom`** | 3000 | 3000 | VROOM Solver |
