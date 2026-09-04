# Route Optimization Stack — OSRM + VROOM (MLD)

RoutePlanningAgent gọi **VROOM** (VRP solver) để tối ưu thứ tự điểm dừng;
VROOM dùng **OSRM** (thuật toán **MLD — Multi-Level Dijkstra**) tính ma trận thời gian di chuyển thực tế.

```
RoutePlanningAgent ──HTTP──► VROOM (:3000) ──HTTP──► OSRM (:5010, MLD)
```

---

## 1. Storage Architecture: Azure Blob Storage

Dataset bản đồ OSRM được lưu trữ và phân phối thông qua **Azure Blob Storage**:

```
Azure Storage Account (stauroradatademo)
└── Container: osrm
    ├── map.osrm
    ├── map.osrm.cells
    ├── map.osrm.cell_metrics
    ├── map.osrm.datasource_names
    ├── map.osrm.ebg
    ├── map.osrm.edges
    ├── map.osrm.enw
    ├── map.osrm.fileIndex
    ├── map.osrm.geometry
    ├── map.osrm.guidance
    ├── map.osrm.icd
    ├── map.osrm.maneuver_overrides
    ├── map.osrm.mldgr
    ├── map.osrm.names
    ├── map.osrm.nbg_nodes
    ├── map.osrm.nodes
    ├── map.osrm.partition
    ├── map.osrm.properties
    ├── map.osrm.ramIndex
    ├── map.osrm.restrictions
    ├── map.osrm.timestamp
    ├── map.osrm.tld
    ├── map.osrm.tls
    ├── map.osrm.turn_duration_penalties
    ├── map.osrm.turn_penalties
    └── map.osrm.turn_weight_penalties
```

---

## 2. Build map data (chạy LOCAL, một lần / mỗi lần cập nhật map)

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

---

## 3. Upload lên Azure Blob Storage

```powershell
# 1. Tạo container (nếu chưa tạo qua Terraform)
az storage container create --name osrm-data --account-name <storage-account-name> --auth-mode login

# 2. Upload toàn bộ bộ tệp map.osrm* lên container
az storage blob upload-batch --source . --pattern "map.osrm*" --destination osrm-data --account-name <storage-account-name> --auth-mode login
```

---

## 4. Chạy stack (Dev / Server / Kubernetes)

### Cách A: Dùng Azure Managed Identity / Azure CLI Login (Khuyên dùng trên AKS / VM)

```powershell
$env:AZURE_STORAGE_ACCOUNT_NAME = "stauroraosrmdev"
$env:AZURE_STORAGE_CONTAINER = "osrm-data"

.\download-data.ps1          # Tải map.osrm* về ./data (tự động bỏ qua nếu đã có cache)
docker compose up -d         # OSRM :5010 + VROOM :3000
```

Trên Linux / CI/CD:
```bash
export AZURE_STORAGE_ACCOUNT_NAME="stauroraosrmdev"
export AZURE_STORAGE_CONTAINER="osrm-data"

./download-data.sh
docker compose up -d
```

### Cách B: Dùng Connection String (Local Dev Fallback)

```powershell
$env:AZURE_STORAGE_CONNECTION_STRING = "<connection-string>"
.\download-data.ps1
docker compose up -d
```

### Smoke Test:

```powershell
# OSRM (Port 5010)
curl "http://localhost:5010/route/v1/driving/106.700,10.776;106.660,10.762"

# VROOM (Port 3000)
curl -X POST http://localhost:3000/ -H "Content-Type: application/json" -d '{
  "vehicles": [{ "id": 1, "profile": "car", "start": [106.700, 10.776] }],
  "jobs": [{ "id": 1, "location": [106.660, 10.762], "service": 300 }],
  "options": { "g": true }
}'
```

---

## 5. Cấu hình RoutePlanningAgent

`appsettings.Development.json`:

```json
"Optimization": {
  "OsrmUrl": "http://localhost:5010",
  "VroomUrl": "http://localhost:3000",
  "TimeoutSeconds": 30
}
```
