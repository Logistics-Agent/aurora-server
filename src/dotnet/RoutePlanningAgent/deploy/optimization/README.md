# Route Optimization Stack — OSRM + VROOM (MLD)

RoutePlanningAgent gọi **VROOM** (VRP solver) để tối ưu thứ tự điểm dừng;
VROOM dùng **OSRM** (thuật toán **MLD — Multi-Level Dijkstra**) tính ma trận thời gian di chuyển thực tế.

```
RoutePlanningAgent ──HTTP──► VROOM (:3000) ──HTTP──► OSRM (:5010, MLD)
```

## 1. Build map data (chạy LOCAL, một lần / mỗi lần cập nhật map)

Tải OSM extract (ví dụ Việt Nam) từ Geofabrik rồi build bằng pipeline **MLD**
(⚠ KHÔNG dùng `osrm-contract` — đó là pipeline CH):

```powershell
# Tải map
curl -o vietnam-latest.osm.pbf https://download.geofabrik.de/asia/vietnam-latest.osm.pbf

# Pipeline MLD (docker, profile car)
docker run -t -v ${PWD}:/data ghcr.io/project-osrm/osrm-backend osrm-extract -p /opt/car.lua /data/vietnam-latest.osm.pbf
docker run -t -v ${PWD}:/data ghcr.io/project-osrm/osrm-backend osrm-partition /data/vietnam-latest.osrm
docker run -t -v ${PWD}:/data ghcr.io/project-osrm/osrm-backend osrm-customize /data/vietnam-latest.osrm

# Đổi tên về "map.osrm*" cho khớp docker-compose (osrm-routed đọc /data/map.osrm)
Get-ChildItem vietnam-latest.osrm* | Rename-Item -NewName { $_.Name -replace "^vietnam-latest", "map" }
```

## 2. Upload lên Azure Blob

```powershell
az storage container create --name osrm-data --connection-string $env:AZURE_STORAGE_CONNECTION_STRING
az storage blob upload-batch --source . --pattern "map.osrm*" --destination osrm-data --connection-string $env:AZURE_STORAGE_CONNECTION_STRING
```

## 3. Chạy stack (máy dev / server)

```powershell
$env:AZURE_STORAGE_CONNECTION_STRING = "<connection-string>"
.\download-data.ps1          # kéo map.osrm* về ./data
docker compose up -d         # OSRM :5010 + VROOM :3000
```

Smoke test:

```powershell
# OSRM
curl "http://localhost:5010/route/v1/driving/106.700,10.776;106.660,10.762"

# VROOM (1 xe + 1 job)
curl -X POST http://localhost:3000/ -H "Content-Type: application/json" -d '{
  "vehicles": [{ "id": 1, "profile": "car", "start": [106.700, 10.776] }],
  "jobs": [{ "id": 1, "location": [106.660, 10.762], "service": 300 }],
  "options": { "g": true }
}'
```

## 4. Cấu hình RoutePlanningAgent

`appsettings.Development.json`:

```json
"Optimization": {
  "OsrmUrl": "http://localhost:5010",
  "VroomUrl": "http://localhost:3000",
  "TimeoutSeconds": 30
}
```

Ghi chú:
- Mô hình hiện tại: **1 vehicle / route**, stop đầu tiên (Sequence nhỏ nhất) là điểm xuất phát,
  các stop còn lại được VROOM tự tối ưu thứ tự. Multi-vehicle là follow-up.
- VROOM trả `distance` (mét) chỉ khi bật `options.g = true` (đã bật trong config + client).
- Điểm dừng nằm ngoài vùng map → VROOM trả `unassigned` → API trả lỗi InvalidArgument kèm tên stop.
