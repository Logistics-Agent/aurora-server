# Aurora GPS Go Load-Generator (`gps-loadgen`)

High-performance, concurrent GPS load generator written in Go for load testing and benchmarking `GpsTrackingService` (.NET & Go).

> Hướng dẫn toàn diện về khởi chạy dịch vụ GPS và kịch bản mô phỏng xem tại: [tools/gps-simulator/README.md](../gps-simulator/README.md).

---

## 1. Tính năng nổi bật

- **Goroutine-per-vehicle Architecture**: Xử lý hàng nghìn phương tiện gửi dữ liệu đồng thời với lượng RAM tối thiểu.
- **Shared gRPC Connection**: Sử dụng 1 kết nối TCP với HTTP/2 Multiplexing, tránh connection churn.
- **100% Deterministic & Reproducible**: Cùng hạt giống `-seed <N>` sinh ra cùng tốc độ, cùng góc lái, cùng tọa độ lệch tuyến và chuỗi bản tin.
- **Tương thích hoàn toàn với C# Simulator**: Cùng chung định dạng báo cáo bảng metrics ở stdout.

---

## 2. Hướng dẫn sử dụng nhanh

### Biên dịch:
```bash
cd tools/gps-loadgen
go build -o bin/gps-loadgen.exe .
```

### Chạy với Load Profile chuẩn (A – E):
```bash
# Profile C (100 xe x 1s = 100 events/sec, trong 300 giây, seed 42)
.\bin\gps-loadgen.exe -profile C -duration 300 -seed 42 -gps http://localhost:6002

# Profile D (500 xe x 1s = 500 events/sec, stress test)
.\bin\gps-loadgen.exe -profile D -duration 300 -seed 42 -gps http://localhost:6002
```

### Tùy biến tham số CLI:
```bash
.\bin\gps-loadgen.exe \
  -vehicles 200 \
  -interval 0.5 \
  -speed 60 \
  -behavior mixed \
  -duration 180 \
  -seed 42 \
  -gps http://localhost:6002 \
  -tenant 01920000-0000-7000-8000-000000000001
```

### Chạy dạng Docker / Kubernetes:
```bash
# Docker build
docker build -t aurora/gps-loadgen:latest -f Dockerfile .

# Kubernetes Job
kubectl apply -f deploy/k8s-job.yaml
```

