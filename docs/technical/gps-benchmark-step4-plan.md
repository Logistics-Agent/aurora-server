# Kế hoạch So sánh Hiệu năng GPS Tracking (.NET vs Go) — Step 4

## 1. Trạng thái Tổng thể các Bước

| Bước | Nội dung | Trạng thái | Ghi chú |
|---|---|:---:|---|
| **Bước 1** | Mở rộng C# GPS simulator chạy nhiều shipment concurrent | **DONE** | Hỗ trợ mode `single`, `auto`, `fleet`, `scenario`, `--profile A-E`, dynamic trajectory, graceful shutdown, unit tests pass 100%. |
| **Bước 2** | Dùng simulator làm functional/load driver cho GpsTracking .NET | **DONE** | 6 functional test scenarios, 5 load profiles chuẩn, tài liệu baseline & bottleneck DB `docs/technical/gps-tracking-benchmark-baseline.md`. |
| **Bước 3** | Tạo Go load-generator (`tools/gps-loadgen`) tương đương C# | **DONE** | Độc lập, goroutine per vehicle, HTTP/2 multiplexing, deterministic PRNG seed, metrics p50/p95/p99 cùng format, unit tests pass. |
| **Bước 4** | So sánh GpsTracking .NET và phiên bản Go trên cùng workload | **IN PROGRESS (BLOCKED)** | **Infrastructure benchmark đã sẵn sàng 100%**. Đang chờ hoàn thiện service `src/go/gps-tracking` để thực thi so sánh đối đầu. |

---

## 2. Kết quả Audit Hiện trạng (Workspace Reality Check)

### 2.1. C# Simulator (`tools/gps-simulator`)
- Điểm bắt đầu: [`Program.cs`](file:///d:/IT/CD/aurora-server/tools/gps-simulator/Program.cs).
- Các mode hỗ trợ:
  - `single`: Mô phỏng 1 shipment cụ thể (backward compatibility).
  - `auto`: Tự động truy vấn `ListShipments` qua gRPC, lọc các shipment đủ điều kiện.
  - `fleet`: Mô phỏng hạm đội $N$ xe với quỹ đạo thực tế hoặc synthetic corridor.
  - `scenario`: Tự động chạy bộ 6 kịch bản kiểm thử hành vi nghiệp vụ.
  - Phím tắt `--profile A|B|C|D|E` tự động map sang số lượng xe và khoảng thời gian chuẩn.

### 2.2. Go Load-Generator (`tools/gps-loadgen`)
- Thư mục: [`tools/gps-loadgen`](file:///d:/IT/CD/aurora-server/tools/gps-loadgen).
- Binary: `bin/gps-loadgen.exe`, Dockerfile và Kubernetes Job manifest `deploy/k8s-job.yaml`.
- Tính năng:
  - Cùng bộ tham số CLI: `-mode`, `-profile`, `-vehicles`, `-interval`, `-speed`, `-behavior`, `-duration`, `-seed`, `-gps`, `-tenant`, `-concurrency`.
  - Concurrency mô hình Goroutine per vehicle, bounded qua channel semaphore.
  - Dùng chung 1 `grpc.ClientConn` đa ghép kênh HTTP/2 (HTTP/2 multiplexing).
  - Thống kê metrics: atomic counters, latency percentiles p50, p95, p99, format xuất bảng kết quả giống hệt C#.

### 2.3. Tính Tái lập của Workload (Reproducibility)
- Cả hai driver đều hỗ trợ cờ `-seed <N>`. Cùng một seed sẽ tạo ra:
  - Cùng chuỗi `vehicle_id` (`sim-veh-0001` -> `sim-veh-NNNN`).
  - Cùng `device_id` (`sim-dev-sim-veh-NNNN`).
  - Cùng định dạng `external_reading_id` (`sim-{vehicleId}-{sequence:08d}`).
  - Cùng độ biến thiên tốc độ ($\pm 4\text{ km/h}$), cùng góc lái (heading) và độ lệch quỹ đạo (deviation drift).
- Đã chuẩn hóa file dataset cố định: [`benchmarks/gps-tracking/data/shipments-manifest.json`](file:///d:/IT/CD/aurora-server/benchmarks/gps-tracking/data/shipments-manifest.json).

### 2.4. Trạng thái Backend GpsTracking Go
- **Hiện trạng:** Thư mục `src/go/` hiện chỉ có `route-planning-agent` và `shared`. **Chưa tồn tại** mã nguồn của service `src/go/gps-tracking`.
- **Thành phần còn thiếu:**
  1. Domain models & validation: `GpsPosition`, `CurrentLocation`, `VehicleShipmentAssignment`, `Geofence`, `MonitoringAlert`.
  2. PostgreSQL Storage: Cùng schema với migration .NET (Positions, CurrentLocations, VehicleShipmentAssignments, Geofences, MonitoringAlerts, OutboxMessages).
  3. gRPC Service Handlers: Triển khai proto contract `GpsTrackingService` (`IngestPosition`, `GetCurrentLocation`, ...).
  4. Outbox Publisher Worker: Quét bảng Outbox và publish `GpsPositionUpdatedEvent` qua RabbitMQ.

### 2.5. Hợp đồng cần Freeze (Contract Freeze)
- **Protobuf Contract:** `protos/gps_tracking.proto` và `protos/shipment_workflow.proto`.
- **Metadata Context:** `x-tenant-id`, `x-user-id`.
- **Database Schema:** Cấu trúc bảng PostgreSQL và kiểu dữ liệu tọa độ `DOUBLE PRECISION` / `NUMERIC` hiện tại của .NET EF Core.
- **Message Broker:** Exchange và routing key của RabbitMQ (`gps.position.updated`, `gps.alert.raised`).

---

## 3. Kế hoạch Chi tiết Step 4: Chuẩn hóa & Thực thi Benchmark

### 3.1. Chuẩn hóa Môi trường Runtime So sánh
Cả .NET và Go phải chạy trong cùng điều kiện hạ tầng:

| Tham số | Cấu hình Chuẩn | Ghi chú |
|---|---|---|
| **CPU Limit** | `500m` (0.5 vCPU) | Khống chế cgroups tương đương trong Docker/K8s |
| **Memory Limit** | `256Mi` | Khống chế cgroups tương đương |
| **Replicas** | `1` | Đơn thể để đo đúng năng lực xử lý bão hòa |
| **PostgreSQL** | Port `5435`, database `aurora_gps_tracking` | Dùng chung 1 DB instance và data snapshot |
| **RabbitMQ** | Port `5672`, vhost `/` | Dùng chung 1 broker instance |
| **gRPC Client Connection** | 1 Shared TCP Connection (HTTP/2 Multiplexing) | Tránh tạo connection mới mỗi request |
| **gRPC Call Deadline** | `5.0s` | Không tự động retry ngầm làm méo phân vị latency |

### 3.2. Chuẩn hóa Test Profiles

| Profile | Số xe (`--vehicles`) | Chu kỳ gửi (`--interval`) | Throughput mục tiêu | Mục đích |
|---|---:|---:|---:|---|
| **Profile A** | 10 | 5.0s | 2 events/s | Kiểm tra tính ổn định gRPC baseline |
| **Profile B** | 50 | 3.0s | ~16.7 events/s | Tải thông thường |
| **Profile C** | 100 | 1.0s | 100 events/s | **Profile chuẩn so sánh chính (Production Baseline)** |
| **Profile D** | 500 | 1.0s | 500 events/s | Đo giới hạn chịu tải DB Connection Pool |
| **Profile E** | 1,000 | 5.0s | 200 events/s | Kiểm tra độ phình bộ nhớ snapshot xe |

### 3.3. Quy trình Thực hiện Đo kiểm (Benchmark Protocol)
Mỗi phiên thử nghiệm phải tuân thủ nghiêm ngặt 3 giai đoạn:
1. **Warm-up Window (60 giây):**
   - Chạy tải ở cùng profile để warm-up: JIT compilation (.NET), connection pool initialization, OS TCP buffers, DB query cache.
   - Dữ liệu đo trong 60 giây này **không** tính vào kết quả báo cáo.
   - Nghỉ làm nguội (cool-down) 10 giây trước khi bắt đầu đo.
2. **Measurement Window (300 giây = 5 phút):**
   - Thu thập liên tục toàn bộ client latency và server metrics.
3. **Số lần lặp (Iterations):**
   - Lặp lại tối thiểu **3 lần** cho mỗi cấu hình để loại trừ nhiễu hệ điều hành/background OS tasks. Lấy giá trị trung bình (Average) và trung vị (Median).

### 3.4. Bảng Chỉ số Đo lường Bắt buộc (Metrics Matrix)

| Chỉ số | Đơn vị | Phương thức thu thập | Ý nghĩa kỹ thuật |
|---|:---:|---|---|
| **Throughput** | events/sec | Client metrics (`SimulationMetrics`) | Khả năng nuốt tải thực tế |
| **Client Latency p50** | ms | Client metrics | Trải nghiệm trung bình của device |
| **Client Latency p95** | ms | Client metrics | Độ ổn định khi có tải |
| **Client Latency p99** | ms | Client metrics | Đỉnh trễ (tail latency / GC pause) |
| **Server CPU Usage** | millicores | Docker / Process stats (`/proc` / cgroups) | Hiệu quả sử dụng vi xử lý |
| **Server RSS Memory** | MiB | Docker / Process stats | Mức độ tiêu thụ RAM thực tế |
| **DB Queries / Event** | count | PostgreSQL `pg_stat_statements` | Hiệu quả thiết kế truy vấn dữ liệu |
| **DB Connection Pool** | active/wait | Npgsql / pgx pool stats | Mức độ nghẽn kết nối database |
| **Outbox Processing Lag** | ms / msgs | RabbitMQ Queue & Outbox table lag | Tốc độ giải phóng hàng đợi sự kiện |
| **Error / Timeout Rate** | % | Client metrics | Tỷ lệ lỗi gRPC |

### 3.5. Lệnh Chạy Benchmark Cố định

Sử dụng bộ kịch bản tự động hóa đã xây dựng tại [`benchmarks/gps-tracking/scripts/run-benchmark.ps1`](file:///d:/IT/CD/aurora-server/benchmarks/gps-tracking/scripts/run-benchmark.ps1):

```powershell
# Chạy đo kiểm baseline .NET
.\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target dotnet -Profile C -Duration 300 -Seed 42

# Chạy đo kiểm đối đầu Go
.\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target go -Profile C -Duration 300 -Seed 42
```

Kết quả tự động ghi vào:
- CSV: `benchmarks/gps-tracking/results/benchmark-results.csv`
- JSON: `benchmarks/gps-tracking/results/summary-<target>-<profile>-<timestamp>.json`

---

## 4. Danh sách Chống Sai lệch Benchmark (Anti-Skew Checklist)

Để đảm bảo kết quả so sánh công bằng và khoa học:
1. **Dùng chung Dataset**: Cả 2 phía bắt buộc đọc chung từ [`shipments-manifest.json`](file:///d:/IT/CD/aurora-server/benchmarks/gps-tracking/data/shipments-manifest.json).
2. **Cùng Seed ngẫu nhiên**: Luôn truyền cùng `--seed 42` để không sai khác về tốc độ hoặc mật độ gửi.
3. **Cùng Database State**: Trước mỗi phiên đo, snapshot lại database PostgreSQL hoặc chạy truncate bảng `Positions`/`MonitoringAlerts` để không bị ảnh hưởng bởi độ lớn dữ liệu tích lũy.
4. **Không Retry ngầm**: Tắt auto-retry ở client driver để phản ánh đúng số lỗi timeout.
5. **Đúng thời gian Warm-up**: Không đo ngay khi vừa khởi động service.
6. **Kiểm tra Index DB**: Đảm bảo cả hai dịch vụ đều dùng chung index `(tenant_id, device_id, external_reading_id)` và `(tenant_id, vehicle_id)`.

---

## 5. Tiêu chí Đánh giá & Điều kiện Rollback

### 5.1. Tiêu chí Go Thắng (Đạt điều kiện Migration)
Việc rewrite một service domain sang Go chỉ được chấp thuận nếu Go runtime đem lại sự vượt trội rõ rệt theo ít nhất một trong các điều kiện sau:
- **Client Latency p95 giảm $\ge 30\%$** ở cùng mức tải.
- **Throughput tối đa tăng $\ge 2\times$** trước khi đạt ngưỡng bão hòa lỗi ($> 0.1\%$).
- **Memory RSS giảm $\ge 40\%$** (tiết kiệm chi phí vận hành container trên cụm).
- **1 replica Go chịu tải gấp $\ge 2\times$** so với 1 replica .NET ở cùng SLA latency $< 50\text{ ms}$.

> **Lưu ý:** Nếu Go chỉ nhanh hơn $5 - 10\%$ nhưng đội ngũ phải bảo trì thêm một codebase mới và duplicate logic nghiệp vụ, **giữ nguyên .NET** là quyết định tối ưu về mặt kiến trúc.

### 5.2. Chiến lược Migration (Shadow & Canary Rollout)
Nếu Go đạt tiêu chí thắng:
- **Phase A (Shadow Traffic):** Triển khai Go service chạy song song. BFF hoặc Envoy gRPC proxy nhân bản (mirror) 100% traffic vị trí vào Go. Dữ liệu Go ghi vào database/schema riêng, không ảnh hưởng sản xuất. Đo tính ổn định và so sánh output 1-1.
- **Phase B (Canary 10%):** Định tuyến 10% xe thật sang Go nhận vị trí và ghi vào DB chính.
- **Phase C (Canary 50%):** Tăng lên 50% lượng xe khi không có lỗi trong 72 giờ.
- **Phase D (Full 100%):** Chuyển 100% traffic cho Go service.
- **Phase E (Fallback Standby):** Giữ container .NET ở trạng thái idle/standby trong 30 ngày trước khi decommission hoàn toàn.

### 5.3. Điều kiện Rollback Ngay lập tức
Rollback về .NET ngay lập tức nếu xảy ra một trong các hiện tượng:
- Tỷ lệ lỗi gRPC của Go vượt quá $0.05\%$.
- Phát hiện rò rỉ dữ liệu giữa các tenant (vi phạm multi-tenant isolation).
- Sai lệch trạng thái geofence alert so với .NET oracle.
- Go process crash do unhandled panic hoặc memory leak.

---

## 6. Kế hoạch Ngắn nhất để Unblock Step 4

Vì `src/go/gps-tracking` hiện chưa tồn tại, các công việc tối thiểu cần thực hiện để mở khóa Step 4 gồm:

```text
Step 4.1: Khởi tạo service src/go/gps-tracking (Module aurora/gps-tracking)
   ├── 4.1.1: Định nghĩa Data Models & DB schema mapping tương thích 100% EF Core
   ├── 4.1.2: Triển khai IngestPositionHandler (Deduplication + Snapshot + Outbox)
   ├── 4.1.3: Triển khai GetCurrentLocationHandler & ListPositionHistoryHandler
   └── 4.1.4: Viết Dockerfile chuẩn 500m CPU / 256Mi RAM

Step 4.2: Nghiệm thu tính tuân thủ Hợp đồng (Contract Compliance Verification)
   └── Chạy dotnet run --project tools/gps-simulator -- --mode scenario --scenario all trỏ vào Go service
       (Bắt buộc PASS 100% 6 functional scenarios mới được bước vào benchmark)

Step 4.3: Chạy Benchmark Đối đầu (.NET vs Go)
   ├── 4.3.1: Chạy .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target dotnet -Profile C
   ├── 4.3.2: Chạy .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target go -Profile C
   └── 4.3.3: Xuất báo cáo so sánh tự động và đưa ra kết luận Migration
```

