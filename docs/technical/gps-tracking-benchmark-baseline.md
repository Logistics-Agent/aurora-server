# GPS Tracking .NET Ingestion Baseline & Bottleneck Analysis

## 1. Mục tiêu

Tài liệu này xác định baseline hiệu năng của `GpsTrackingService` (.NET 10) trước khi tiến hành benchmark so sánh với triển khai Go.

## 2. Resource Baseline (.NET Service)

Theo cấu hình triển khai Kubernetes (`values.yaml`):
- **CPU Request / Limit**: `250m` / `500m` (0.5 vCPU)
- **Memory Request / Limit**: `128Mi` / `256Mi`
- **Replicas**: 1 replica trong giai đoạn benchmark đơn thể
- **Runtime**: .NET 10 (Kestrel HTTP/2 gRPC Server)
- **Database**: PostgreSQL 16 (local development port `5435`, db `aurora_gps_tracking`)
- **Message Broker**: RabbitMQ (Outbox processor chu kỳ 1s)

## 3. Phân tích Bottleneck trong Ingestion Path

Tại `PositionIngestionService.cs`, mỗi cuộc gọi `IngestPosition` qua gRPC thực hiện chuỗi thao tác cơ sở dữ liệu sau:

```text
gRPC IngestPositionRequest
   │
   ├─► 1. Deduplication Check
   │      SELECT * FROM "Positions"
   │      WHERE "TenantId" = @t AND "DeviceId" = @d AND "ExternalReadingId" = @ext;
   │
   ├─► 2. Active Vehicle Assignment Lookup
   │      SELECT * FROM "VehicleShipmentAssignments"
   │      WHERE "TenantId" = @t AND "VehicleId" = @v AND "EndedAt" IS NULL
   │      ORDER BY "AssignedAt" DESC LIMIT 1;
   │
   ├─► 3. Current Location Snapshot Query
   │      SELECT * FROM "CurrentLocations"
   │      WHERE "TenantId" = @t AND "VehicleId" = @v;
   │
   ├─► 4. Monitoring & Geofence Evaluation (nếu vị trí tiến về trước)
   │      SELECT * FROM "Geofences" WHERE ...
   │      SELECT * FROM "GeofencePresences" WHERE ...
   │
   └─► 5. Database Commit (Transaction)
          INSERT INTO "Positions" (...)
          UPDATE/INSERT INTO "CurrentLocations" (...)
          INSERT INTO "OutboxMessages" ("GpsPositionUpdatedEvent", ...)
          [Tùy chọn: INSERT INTO "MonitoringAlerts" nếu vi phạm]
```

### Nhận xét về số truy vấn DB:
- Mỗi GPS event trung bình kích hoạt **4 đến 6 truy vấn SQL** và **1 transaction commit**.
- Ở mức 500 events/sec (Profile D), PostgreSQL phải chịu tải khoảng **2,000 - 3,000 queries/sec**.
- Connection pool của Npgsql EF Core và Outbox lock contention là 2 điểm nghẽn chính khi scale concurrency.

## 4. Định nghĩa các Load Profiles chuẩn

Simulator hỗ trợ 5 profile cố định:

| Profile | Số xe (`--vehicles`) | Khoảng thời gian (`--interval`) | Tần suất lý thuyết (events/sec) | Mục đích thử nghiệm |
|---|---|---|---|---|
| **Profile A** | 10 | 5.0s | 2 evt/s | Smoke test / Functional verification |
| **Profile B** | 50 | 3.0s | ~16.7 evt/s | Low load / Stability check |
| **Profile C** | 100 | 1.0s | 100 evt/s | Medium concurrency / Baseline production |
| **Profile D** | 500 | 1.0s | 500 evt/s | High throughput / DB Connection pool stress |
| **Profile E** | 1,000 | 5.0s | 200 evt/s | High vehicle count / In-memory tracking scale |

## 5. Lệnh chạy Baseline Benchmark

Chạy kịch bản Profile C trong 300 giây (5 phút) với seed cố định:

```bash
# C# Simulator
dotnet run --project tools/gps-simulator -- --profile C --duration 300 --seed 42

# Go Load-Generator (tương đương 100% workload)
./tools/gps-loadgen/bin/gps-loadgen -profile C -duration 300s -seed 42
```

## 6. Functional Verification Scenarios

Bộ scenario tự động kiểm thử 6 hành vi cốt lõi của GpsTracking:
```bash
dotnet run --project tools/gps-simulator -- --mode scenario --scenario all
```
1. `normal`: Kiểm tra current location tiến dần về trước và khớp vị trí mới nhất.
2. `duplicate`: Kiểm tra tính idempotent khi gửi trùng `external_reading_id`.
3. `late`: Kiểm tra reading đến muộn được lưu vào history nhưng không làm lùi snapshot hiện tại.
4. `geofence`: Tạo geofence bán kính 1000m, kiểm tra chuyển trạng thái vào/ra khỏi vòng tròn.
5. `stop`: Kiểm tra gửi chuỗi điểm dừng (speed = 0) phát hiện trạng thái stationary.
6. `multitenant`: Đảm bảo Tenant B không thể truy xuất vị trí xe của Tenant A.

