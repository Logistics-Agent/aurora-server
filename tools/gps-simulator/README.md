# Aurora GPS Tracking & Simulator Guide

Tài liệu hướng dẫn toàn diện về cách khởi chạy hệ thống **GPS Tracking Service** (.NET & Go) và sử dụng bộ đôi công cụ mô phỏng tải **C# GPS Simulator** & **Go Load-Generator** để thực hiện kiểm thử chức năng (functional test) và đo kiểm hiệu năng (benchmark).

---

## Mục lục

1. [Kiến trúc Tổng quan](#1-kiến-trúc-tổng-quan)
2. [Khởi chạy GPS Tracking Service](#2-khởi-chạy-gps-tracking-service)
   - [2.1. Hạ tầng phụ thuộc (PostgreSQL & RabbitMQ)](#21-hạ-tầng-phụ-thuộc-postgresql--rabbitmq)
   - [2.2. Chạy GpsTracking Service .NET](#22-chạy-gpstracking-service-net)
   - [2.3. Chạy GpsTracking Service Go](#23-chạy-gpstracking-service-go)
3. [Hướng dẫn sử dụng C# GPS Simulator](#3-hướng-dẫn-sử-dụng-c-gps-simulator)
   - [3.1. Các chế độ hoạt động (Modes)](#31-các-chế-độ-hoạt-động-modes)
   - [3.2. Bảng tham số dòng lệnh (CLI Options)](#32-bảng-tham-số-dòng-lệnh-cli-options)
   - [3.3. Các kịch bản kiểm thử chức năng (Scenario Mode)](#33-các-kịch-bản-kiểm-thử-chức-năng-scenario-mode)
   - [3.4. Các Load Profiles chuẩn hóa (A – E)](#34-các-load-profiles-chuẩn-hóa-a--e)
4. [Hướng dẫn sử dụng Go Load-Generator](#4-hướng-dẫn-sử-dụng-go-load-generator)
   - [4.1. Biên dịch và thực thi Binary](#41-biên-dịch-và-thực-thi-binary)
   - [4.2. Chạy qua Docker hoặc Kubernetes](#42-chạy-qua-docker-hoặc-kubernetes)
5. [Quy trình Benchmark So sánh .NET và Go (1-Click)](#5-quy-trình-benchmark-so-sánh-net-và-go-1-click)
6. [Ý nghĩa các Chỉ số Đo lường (Metrics Report)](#6-ý-nghĩa-các-chỉ-số-đo-lường-metrics-report)
7. [Xử lý Sự cố Thường gặp (Troubleshooting)](#7-xử-lý-sự-cố-thường-gặp-troubleshooting)

---

## 1. Kiến trúc Tổng quan

```text
┌────────────────────────────────────────────────────────────────────────┐
│                          WORKLOAD GENERATORS                           │
│                                                                        │
│   [C# Simulator] (tools/gps-simulator)                                 │
│   • Điểm bắt đầu: Program.cs                                           │
│   • Mục đích: Functional testing, kịch bản nghiệp vụ, demo, single/fleet│
│                                                                        │
│   [Go Load-Generator] (tools/gps-loadgen)                              │
│   • Binary: bin/gps-loadgen.exe                                        │
│   • Mục đích: High concurrency, high throughput, benchmark so sánh     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ gRPC Protocol (protos/gps_tracking.proto)
                                    │ Header: x-tenant-id, x-user-id
                                    │ Reading: sim-{vehicleId}-{seq:08d}
                                    ▼
┌───────────────────────────────────┴────────────────────────────────────┐
│                       GPS TRACKING BACKEND TARGETS                     │
│                                                                        │
│   [.NET Service] src/dotnet/GpsTracking     (gRPC :6002)               │
│   [Go Service]   src/go/gps-tracking         (gRPC :6006 hoặc :6002)   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ PostgreSQL Schema & RabbitMQ
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        INFRASTRUCTURE SERVICES                         │
│   • PostgreSQL: localhost:5435/aurora_gps_tracking                     │
│   • RabbitMQ:   localhost:5672 (Outbox processing)                     │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Khởi chạy GPS Tracking Service

### 2.1. Hạ tầng phụ thuộc (PostgreSQL & RabbitMQ)

Khởi động PostgreSQL và RabbitMQ qua Docker Compose:

```bash
docker compose -f deploy/local/docker-compose.yml up -d postgres rabbitmq
```

- **PostgreSQL**: `localhost:5435` (User: `aurora_admin`, Pass: `aurora_secure_password_2026!`, Database: `gps_tracking_db` hoặc `aurora_gps_tracking`)
- **RabbitMQ**: `localhost:5672`, Management UI: `http://localhost:15672` (User/Pass: `aurora_admin` / `aurora_secure_password_2026!`)

---

### 2.2. Chạy GpsTracking Service .NET

Service .NET lắng nghe gRPC tại port `6002`:

```bash
# Cách 1: Chạy trực tiếp qua dotnet CLI
cd src/dotnet/GpsTracking
dotnet run

# Cách 2: Chạy qua Docker Compose
docker compose -f deploy/local/docker-compose.yml up -d gps-tracking
```

Kiểm tra service đã sẵn sàng:
- Endpoint gRPC: `http://localhost:6002`

---

### 2.3. Chạy GpsTracking Service Go

Service Go lắng nghe gRPC tại port `6006` (khi chạy song song với .NET) hoặc `6002` (khi chạy độc lập):

```bash
# Cách 1: Chạy trực tiếp qua Go CLI
cd src/go/gps-tracking
go run ./cmd/server

# Cách 2: Chạy binary đã biên dịch
.\src\go\gps-tracking\bin\gps-tracking-server.exe

# Cách 3: Chạy qua Docker Compose (được cấu hình port 6006:6002)
docker compose -f deploy/local/docker-compose.yml --profile benchmark up -d gps-tracking-go
```

---

## 3. Hướng dẫn sử dụng C# GPS Simulator

C# Simulator đặt tại `tools/gps-simulator/`. Điểm bắt đầu thực thi là [`Program.cs`](file:///d:/IT/CD/aurora-server/tools/gps-simulator/Program.cs).

### 3.1. Các chế độ hoạt động (Modes)

#### Chế độ 1: Single Shipment (Tương thích ngược)
Chạy mô phỏng 1 xe cho 1 shipment UUID chỉ định:
```bash
dotnet run --project tools/gps-simulator -- --shipment 01920000-0000-7000-8000-000000000001
```

#### Chế độ 2: Auto Discovery
Tự động gọi RPC `ListShipments` đến Shipment Workflow Service, tự phát hiện xe và tuyến đường:
```bash
dotnet run --project tools/gps-simulator -- --mode auto
```

#### Chế độ 3: Fleet (Hạm đội nhiều xe đồng thời)
Mô phỏng đồng thời 100 xe chạy trong 300 giây với hạt giống ngẫu nhiên (seed) 42:
```bash
dotnet run --project tools/gps-simulator -- --mode fleet --vehicles 100 --interval 1 --duration 300 --seed 42
```
Hoặc nạp danh sách xe từ file manifest JSON:
```bash
dotnet run --project tools/gps-simulator -- --mode fleet --shipments benchmarks/gps-tracking/data/shipments-manifest.json
```

#### Chế độ 4: Load Profile Shortcut (Khuyên dùng)
Sử dụng các profile chuẩn hóa A – E chỉ bằng một cờ `--profile`:
```bash
dotnet run --project tools/gps-simulator -- --profile C --duration 300 --seed 42
```

#### Chế độ 5: Scenario Mode (Kiểm tra chức năng nghiệp vụ)
Tự động chạy bộ kịch bản nghiệm thu tính tuân thủ hợp đồng của service:
```bash
dotnet run --project tools/gps-simulator -- --mode scenario --scenario all
```

---

### 3.2. Bảng tham số dòng lệnh (CLI Options)

| Tham số | Giá trị mặc định | Giải thích |
|---|---|---|
| `--mode` | `auto` (hoặc `single` nếu có `--shipment`) | Chế độ: `single`, `auto`, `fleet`, `scenario` |
| `--profile` | *(không có)* | Phím tắt cấu hình profile: `A`, `B`, `C`, `D`, `E` |
| `--scenario` | `all` | Kịch bản kiểm thử: `all`, `normal`, `duplicate`, `late`, `geofence`, `stop`, `multitenant` |
| `--shipment` | *(không có)* | UUID của shipment mục tiêu (bắt buộc khi chạy `single`) |
| `--shipments` | *(không có)* | Đường dẫn file JSON chứa danh sách shipment/xe |
| `--vehicles` | `10` | Số lượng xe máy ảo mô phỏng đồng thời |
| `--interval` | `3` (giây) | Khoảng thời gian giữa 2 lần gửi telemetry (hỗ trợ số thực như `0.5`, `1`, `3`) |
| `--speed` | `50` (km/h) | Tốc độ cơ sở của xe trên hành trình |
| `--behavior` | `normal` | Hành vi: `normal` (chuẩn), `mixed` (hỗn hợp), `deviation` (lệch tuyến), `signal-loss` (mất sóng) |
| `--duration` | *(chạy hết tuyến)* | Giới hạn thời gian mô phỏng theo giây (ví dụ: `300` là 5 phút) |
| `--seed` | *(ngẫu nhiên)* | Số nguyên cố định bộ sinh số giả ngẫu nhiên (đảm bảo tính tái lập 100%) |
| `--gps`, `--grpc` | `http://localhost:6002` | Địa chỉ gRPC của GPS Tracking Service (hoặc `$GPS_GRPC_URL`) |
| `--shipment-grpc`| `http://localhost:6000` | Địa chỉ gRPC của Shipment Service (hoặc `$SHIPMENT_GRPC_URL`) |
| `--route-grpc`   | `http://localhost:5002` | Địa chỉ gRPC của Route Planning Service (hoặc `$ROUTE_GRPC_URL`) |
| `--tenant`       | `01920000-...` | Tenant ID định danh tổ chức |
| `--user-id`      | `01910000-...` | User ID người thực hiện |
| `--auto-assign`  | `false` | Tự sinh `vehicle_id` nếu shipment chưa được gán xe |
| `--concurrency`  | `1000` | Giới hạn số lượng task chạy đồng thời tối đa |
| `-h`, `--help`   | | Xem trợ giúp cú pháp |

---

### 3.3. Các kịch bản kiểm thử chức năng (Scenario Mode)

Chế độ Scenario dùng để kiểm tra tính đúng đắn của logic nghiệp vụ theo `docs/technical/gps-tracking-spec.md`:

```bash
# Kiểm tra toàn bộ 6 kịch bản trên service .NET
dotnet run --project tools/gps-simulator -- --mode scenario --scenario all --gps http://localhost:6002

# Kiểm tra toàn bộ 6 kịch bản trên service Go
dotnet run --project tools/gps-simulator -- --mode scenario --scenario all --gps http://localhost:6006
```

| Tên Scenario | Mô tả kiểm tra | Kết quả mong đợi |
|---|---|---|
| `normal` | Gửi chuỗi vị trí tăng dần $P_1 \to P_2 \to P_3$ | Snapshot `CurrentLocation` khớp chính xác tọa độ $P_3$. |
| `duplicate` | Gửi liên tiếp 2 reading trùng `external_reading_id` | Xử lý idempotent thành công, không tạo duplicate row trong DB. |
| `late` | Gửi điểm hiện tại $T_0$, sau đó gửi điểm trễ $T_0 - 5\text{ phút}$ | Điểm cũ được lưu vào history nhưng `CurrentLocation` **không bị lùi**. |
| `geofence` | Tạo geofence 1000m, gửi vị trí ngoài $\to$ trong $\to$ ngoài | Chuyển trạng thái chính xác, phát sinh alert `GeofenceEntered` / `GeofenceExited`. |
| `stop` | Gửi liên tục các điểm đứng yên (`speed = 0`) | Phát hiện trạng thái xe dừng bất thường (`AbnormalStop`). |
| `multitenant` | Tenant A gửi vị trí, Tenant B thực hiện query | Tenant B nhận `NotFound` (cách ly tenant tuyệt đối). |

---

### 3.4. Các Load Profiles chuẩn hóa (A – E)

| Profile | Số xe (`--vehicles`) | Chu kỳ gửi (`--interval`) | Throughput mục tiêu | Mục đích thử nghiệm |
|---|---:|---:|---:|---|
| **Profile A** | 10 | 5.0s | 2 events/s | Smoke test / Kiểm tra kết nối cơ bản |
| **Profile B** | 50 | 3.0s | ~16.7 events/s | Kiểm tra độ ổn định tải thấp |
| **Profile C** | 100 | 1.0s | 100 events/s | **Profile chuẩn so sánh chính (Production Baseline)** |
| **Profile D** | 500 | 1.0s | 500 events/s | Stress test cơ sở dữ liệu & connection pool |
| **Profile E** | 1,000 | 5.0s | 200 events/s | Đo mức độ phình bộ nhớ snapshot $N$ xe |

---

## 4. Hướng dẫn sử dụng Go Load-Generator

Bộ phát tải Go đặt tại [`tools/gps-loadgen`](file:///d:/IT/CD/aurora-server/tools/gps-loadgen) được tối ưu bằng Goroutines và shared HTTP/2 connection pooling để đạt throughput tối đa.

### 4.1. Biên dịch và thực thi Binary

```bash
# 1. Biên dịch binary (chỉ cần chạy lần đầu)
cd tools/gps-loadgen
go build -o bin/gps-loadgen.exe .

# 2. Chạy với Profile C trong 300s
.\bin\gps-loadgen.exe -profile C -duration 300 -seed 42 -gps http://localhost:6002

# 3. Tùy biến tham số
.\bin\gps-loadgen.exe -vehicles 500 -interval 1.0 -duration 300 -seed 42 -gps http://localhost:6002
```

### 4.2. Chạy qua Docker hoặc Kubernetes

```bash
# Build Docker image
docker build -t aurora/gps-loadgen:latest -f tools/gps-loadgen/Dockerfile tools/gps-loadgen

# Chạy dạng Kubernetes Job
kubectl apply -f tools/gps-loadgen/deploy/k8s-job.yaml
```

---

## 5. Quy trình Benchmark So sánh .NET và Go (1-Click)

Hệ thống đã tích hợp sẵn kịch bản PowerShell tự động hóa tại [`benchmarks/gps-tracking/scripts/run-benchmark.ps1`](file:///d:/IT/CD/aurora-server/benchmarks/gps-tracking/scripts/run-benchmark.ps1).

Script này tự động:
1. Chạy **Warm-up 60s** (làm nóng JIT, khởi tạo pool kết nối, buffer OS).
2. Nghỉ làm nguội 10s.
3. Chạy **3 lần lặp (Iterations)**, mỗi lần 300s với cùng `seed = 42`.
4. Thu thập và xuất kết quả ra file CSV và JSON.

### Lệnh thực thi:

```powershell
# Đo kiểm Baseline cho GpsTracking .NET (Port 6002)
powershell -ExecutionPolicy Bypass -File .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target dotnet -Profile C -Duration 300 -Seed 42 -GpsUrl http://localhost:6002

# Đo kiểm Đối đầu cho GpsTracking Go (Port 6006)
powershell -ExecutionPolicy Bypass -File .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target go -Profile C -Duration 300 -Seed 42 -GpsUrl http://localhost:6006
```

Kết quả được tự động lưu vào:
- **CSV**: `benchmarks/gps-tracking/results/benchmark-results.csv`
- **JSON**: `benchmarks/gps-tracking/results/summary-<target>-<profile>-<timestamp>.json`

---

## 6. Ý nghĩa các Chỉ số Đo lường (Metrics Report)

Cuối mỗi phiên chạy, cả C# Simulator và Go Load-Generator đều xuất bảng báo cáo:

```text
======================= SIMULATION METRICS REPORT =======================
Total Vehicles        : 100
Duration              : 300.05s
Total Events Sent     : 30000
Successful Events     : 29998
Failed Events         : 2
RPC Timeout Count     : 0
Retry Count           : 0
Success Rate          : 99.99%
Throughput            : 99.98 events/sec
Client Latency p50    : 8.40 ms
Client Latency p95    : 16.20 ms
Client Latency p99    : 28.50 ms
=========================================================================
```

| Chỉ số | Đơn vị | Ý nghĩa kỹ thuật |
|---|:---:|---|
| **Total Vehicles** | xe | Số lượng phương tiện ảo cùng gửi dữ liệu đồng thời. |
| **Duration** | giây | Tổng thời gian đo lường thực tế. |
| **Total Events Sent** | tin | Tổng số bản tin vị trí đã gửi đi. |
| **Throughput** | events/sec | Tần suất xử lý thành công thực tế của backend service. |
| **Client Latency p50** | ms | Thời gian phản hồi trung vị (50% requests nhanh hơn mức này). |
| **Client Latency p95** | ms | Phân vị 95% độ trễ (đo lường độ ổn định khi chịu tải). |
| **Client Latency p99** | ms | Phân vị 99% (đo lường đuôi trễ - tail latency do GC pause hoặc DB locks). |
| **Success Rate** | % | Tỷ lệ bản tin được xử lý thành công không lỗi. |
| **RPC Timeout Count** | lần | Số lượng request vượt quá deadline 5.0 giây. |

---

## 7. Xử lý Sự cố Thường gặp (Troubleshooting)

### Lỗi 1: `SocketException: No connection could be made because the target machine actively refused it`
- **Nguyên nhân:** Service GpsTracking chưa được bật hoặc đang lắng nghe trên cổng khác.
- **Khắc phục:**
  - Kiểm tra service .NET đã chạy trên port `6002` chưa: `Test-NetConnection -Port 6002 -ComputerName localhost`.
  - Nếu chạy Go trên port `6006`, đảm bảo đã truyền cờ `--gps http://localhost:6006`.

### Lỗi 2: `Status(StatusCode="InvalidArgument", Detail="x-tenant-id metadata header is required")`
- **Nguyên nhân:** Lời gọi gRPC thiếu header xác thực tenant.
- **Khắc phục:** Mặc định simulator tự động truyền `x-tenant-id`. Nếu gọi qua công cụ ngoài (như Postman/grpcurl), cần thêm header:
  ```text
  x-tenant-id: 01920000-0000-7000-8000-000000000001
  x-user-id: 01910000-0000-7000-8000-000000000001
  ```

### Lỗi 3: `Script cannot be loaded because running scripts is disabled on this system` (PowerShell)
- **Nguyên nhân:** Chính sách ExecutionPolicy của Windows chặn chạy script `.ps1`.
- **Khắc phục:** Thêm cờ `-ExecutionPolicy Bypass` khi gọi lệnh:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 ...
  ```

### Lỗi 4: `42P01: relation "gps_positions" does not exist`
- **Nguyên nhân:** Database PostgreSQL chưa được tạo bảng.
- **Khắc phục:** Khởi động service .NET một lần để EF Core áp dụng migration, hoặc khởi động service Go (Go service tự động gọi `AutoMigrate` để tạo đủ các bảng và index nếu chưa có).

