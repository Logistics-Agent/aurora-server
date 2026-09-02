# Runbook test Notification FCM và GPS local

> **Lưu ý bảo mật:** JSON service-account của Firebase Admin chỉ được dùng ở
> backend. Không đặt file này vào `appsettings*.json`, source code, frontend hoặc
> commit Git.

Đặt file đã được Git ignore tại:

```text
secrets/firebase/aurora-notification-admin.json
```

Chạy Notification bằng biến môi trường:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Redis__Host='localhost:6379'
export ServiceAuth__AllowedServiceId='staff-bff'
export ServiceAuth__ApiKey='local-notification-key'
export Firebase__Enabled=true
export Firebase__CredentialsPath="$PWD/secrets/firebase/aurora-notification-admin.json"
dotnet run --project src/dotnet/Notification/Notification.csproj
```

Chạy Staff BFF với cùng service key:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Redis__Host='localhost:6379'
export Grpc__Notification__ServiceApiKey='local-notification-key'
dotnet run --project src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj
```

Frontend chỉ gọi Staff BFF. BFF yêu cầu JWT hợp lệ và permission
`notifications:access`, sau đó truyền metadata user/tenant cùng credential của
Notification qua gRPC. Notification kiểm tra credential service trước rồi dùng
user/tenant để giới hạn device, subscription và các truy vấn notification.

Frontend phải đăng ký FCM registration token qua
`POST /api/v1/notifications/devices`, subscribe the user to a shipment through
`POST /api/v1/notifications/subscriptions/shipments/{shipmentId}`, and handle
the FCM data fields `notificationId`, `type`, `shipmentId`, and `actionUrl`.

Database của Notification phải có migration chain đúng với model hiện tại. Kiểm
tra trước khi chạy migration:

```bash
dotnet ef migrations list \
  --project src/dotnet/Notification/Notification.csproj \
  --startup-project src/dotnet/Notification/Notification.csproj
```

Không chạy migration mới trên database có lịch sử schema Notification cũ nếu
chưa review baseline/data migration.

## Smoke test gRPC local đầy đủ

Phần này kiểm tra GPS, Notification và popup Firebase thật trên máy local.
`docker-compose.dev.yml` chỉ chạy hạ tầng; các service .NET vẫn chạy bằng
`dotnet run`.

### Chuẩn bị và khởi động hạ tầng

Chạy từ thư mục gốc của repository:

```bash
cd /home/kaito/project/aurora-server
docker compose -f docker-compose.dev.yml up -d
docker compose -f docker-compose.dev.yml ps
```

GPS và Shipment Workflow là gRPC chạy trên plaintext HTTP/2. Cài `grpcurl`
trước khi chạy các lệnh bên dưới. Trên Linux có thể cài bằng Snap hoặc dùng
Docker:

```bash
sudo snap install --edge grpcurl
grpcurl --version
```

Bản Snap hiện có ở channel `edge`, nên không dùng lệnh `sudo snap install
grpcurl` mặc định. Nếu cài bằng Snap bị giới hạn quyền đọc thư mục project, hãy
copy các file proto cần test sang `/tmp` hoặc dùng binary native từ trang
Releases của grpcurl.

Nếu user hiện tại có quyền dùng Docker:

```bash
docker pull fullstorydev/grpcurl:latest
```

Khi dùng Docker trên Linux, thêm `--network host` và mount thư mục `protos` của
repository vào `/protos`. Các ví dụ bên dưới giả định binary `grpcurl` native đã
có trong `PATH`.

Các địa chỉ quan trọng:

| Service | Address |
| --- | --- |
| Shipment Workflow gRPC | `localhost:6000` |
| Notification gRPC | `localhost:6001` |
| GPS Tracking gRPC | `localhost:6002` |
| Notification PostgreSQL | `localhost:5434` |
| GPS PostgreSQL | `localhost:5435` |
| Redis | `localhost:6379` |
| RabbitMQ | `localhost:5672` |
| RabbitMQ management | `http://localhost:15672` |

Thông tin đăng nhập RabbitMQ là `aurora` / `aurora_dev`.

Chạy migration:

```bash
Redis__Host=localhost:6379 dotnet ef database update \
  --project src/dotnet/GpsTracking/GpsTracking.csproj \
  --startup-project src/dotnet/GpsTracking/GpsTracking.csproj

dotnet ef database update \
  --project src/dotnet/Notification/Notification.csproj \
  --startup-project src/dotnet/Notification/Notification.csproj
```

`Redis__Host` là bắt buộc vì shared startup đọc `Redis:Host`, trong khi cấu hình
Development hiện tại của GPS vẫn còn key cũ `Redis:ConnectionString`.

### Chạy các service

Shipment Workflow, cần cho test luồng event đến Notification:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Redis__Host='localhost:6379'
dotnet run --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

GPS Tracking:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Redis__Host='localhost:6379'
# Có thể giảm threshold khi test signal-loss/abnormal-stop
export GpsMonitoring__SignalLossThreshold='00:00:20'
export GpsMonitoring__SignalLossScanInterval='00:00:05'
export GpsMonitoring__AbnormalStopDuration='00:00:20'
dotnet run --project src/dotnet/GpsTracking/GpsTracking.csproj
```

Notification với Firebase Admin:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Redis__Host='localhost:6379'
export ServiceAuth__AllowedServiceId='staff-bff'
export ServiceAuth__ApiKey='local-notification-key'
export Firebase__Enabled=true
export Firebase__CredentialsPath="$PWD/secrets/firebase/aurora-notification-admin.json"
dotnet run --project src/dotnet/Notification/Notification.csproj
```

Kiểm tra health của Notification:

```bash
curl http://localhost:6001/health
curl http://localhost:6001/ready
```

GPS là gRPC server chỉ nhận HTTP/2. Vì vậy request HTTP/1.x thông thường có thể
trả về `An HTTP/1.x request was sent to an HTTP/2 only endpoint.` Điều đó nghĩa
là port đã reachable, nhưng GPS không có REST health endpoint. Hãy kiểm tra GPS
bằng một lời gọi gRPC thật trong phần test bên dưới, ví dụ
`GpsTrackingService/GetCurrentLocation`.

### Metadata gRPC dùng chung và các hàm hỗ trợ

```bash
export USER_ID='01910000-0000-7000-8000-000000000001'
export TENANT_ID='01920000-0000-7000-8000-000000000001'
export NOTIFICATION_KEY='local-notification-key'

GPS_META=(
  -H "x-user-id: $USER_ID"
  -H "x-tenant-id: $TENANT_ID"
)

NOTIFICATION_META=(
  -H "x-service-id: staff-bff"
  -H "x-service-api-key: $NOTIFICATION_KEY"
  -H "x-user-id: $USER_ID"
  -H "x-tenant-id: $TENANT_ID"
)

gps_grpc() {
  grpcurl -plaintext -import-path protos -proto gps_tracking.proto \
    "${GPS_META[@]}" -d "$2" localhost:6002 "$1"
}

notification_grpc() {
  grpcurl -plaintext -import-path protos -proto notification.proto \
    "${NOTIFICATION_META[@]}" -d "$2" localhost:6001 "$1"
}

shipment_grpc() {
  grpcurl -plaintext -import-path protos -proto shipment_workflow.proto \
    "${GPS_META[@]}" -d "$2" localhost:6000 "$1"
}
```

Các header identity mô phỏng một gRPC caller đáng tin cậy trong local. Chúng
không thay thế JWT/session authentication của BFF HTTP API. Không expose các
port gRPC plaintext này ra ngoài môi trường local.

## Các luồng test GPS

### Gửi một vị trí hợp lệ

```bash
gps_grpc GpsTrackingService/IngestPosition '{
  "externalReadingId": "reading-demo-001",
  "deviceId": "gps-device-demo-001",
  "vehicleId": "vehicle-demo-001",
  "latitude": 10.7769,
  "longitude": 106.7009,
  "speedKph": 35,
  "headingDegrees": 90,
  "accuracyMeters": 5,
  "recordedAt": "2026-08-31T05:00:00Z"
}'
```

Response mong đợi có `id`, tọa độ, speed và heading.

### Kiểm tra idempotency

Gửi lại request trước với cùng `externalReadingId`. GPS phải trả về position
đã tồn tại, không tạo thêm position hoặc outbox record.

### Lấy vị trí hiện tại theo vehicle

```bash
gps_grpc GpsTrackingService/GetCurrentLocation \
  '{"vehicleId":"vehicle-demo-001"}'
```

### Gửi vị trí thứ hai

```bash
gps_grpc GpsTrackingService/IngestPosition '{
  "externalReadingId": "reading-demo-002",
  "deviceId": "gps-device-demo-001",
  "vehicleId": "vehicle-demo-001",
  "latitude": 10.7800,
  "longitude": 106.7050,
  "speedKph": 42,
  "headingDegrees": 110,
  "accuracyMeters": 4,
  "recordedAt": "2026-08-31T05:02:00Z"
}'
```

Gọi lại `GetCurrentLocation`; vị trí hiện tại phải được cập nhật.

### Lấy lịch sử vị trí

```bash
gps_grpc GpsTrackingService/ListPositionHistory '{
  "vehicleId": "vehicle-demo-001",
  "from": "2026-08-31T04:00:00Z",
  "to": "2026-08-31T06:00:00Z",
  "page": 1,
  "pageSize": 50
}'
```

Contract hiện tại yêu cầu cả `from` và `to`.

### Tạo và liệt kê geofence

```bash
gps_grpc GpsTrackingService/CreateGeofence '{
  "name": "Demo Port",
  "latitude": 10.7769,
  "longitude": 106.7009,
  "radiusMeters": 100,
  "vehicleId": "vehicle-demo-001"
}'
```

Lưu ID trả về nếu cần bật/tắt geofence:

```bash
export GEOFENCE_ID='GEOFENCE_UUID'
gps_grpc GpsTrackingService/ListGeofences \
  '{"includeInactive":true}'
```

### Kích hoạt sự kiện vào và ra khỏi geofence

Gửi một vị trí mới nằm trong geofence:

```bash
gps_grpc GpsTrackingService/IngestPosition '{
  "externalReadingId": "reading-geofence-inside",
  "deviceId": "gps-device-demo-001",
  "vehicleId": "vehicle-demo-001",
  "latitude": 10.7770,
  "longitude": 106.7010,
  "speedKph": 20,
  "headingDegrees": 90,
  "recordedAt": "2026-08-31T05:03:00Z"
}'
```

Sau đó gửi một vị trí nằm ngoài geofence:

```bash
gps_grpc GpsTrackingService/IngestPosition '{
  "externalReadingId": "reading-geofence-outside",
  "deviceId": "gps-device-demo-001",
  "vehicleId": "vehicle-demo-001",
  "latitude": 10.7800,
  "longitude": 106.7050,
  "speedKph": 20,
  "headingDegrees": 90,
  "recordedAt": "2026-08-31T05:04:00Z"
}'
```

Lấy các alert `GeofenceExited` đang active:

```bash
gps_grpc GpsTrackingService/ListMonitoringAlerts '{
  "alertType": "GeofenceExited",
  "status": "Active",
  "page": 1,
  "pageSize": 20
}'
```

Resolve một alert:

```bash
export ALERT_ID='ALERT_UUID'
gps_grpc GpsTrackingService/ResolveMonitoringAlert \
  "{\"id\":\"$ALERT_ID\"}"
```

### Test signal loss và abnormal stop nhanh hơn

Mặc định signal loss là 5 phút và abnormal stop là 15 phút. Restart GPS với
threshold ngắn hơn, chỉ dùng cho Development:

```bash
export GpsMonitoring__SignalLossThreshold='00:00:20'
export GpsMonitoring__SignalLossScanInterval='00:00:05'
export GpsMonitoring__AbnormalStopDuration='00:00:20'
dotnet run --project src/dotnet/GpsTracking/GpsTracking.csproj
```

Các rule này cần vehicle assignment hợp lệ và current location.

## Các luồng test Notification gRPC

### Kiểm tra service authentication

Request sau phải fail với `Unauthenticated` vì thiếu service credential:

```bash
grpcurl -plaintext -import-path protos -proto notification.proto \
  -H "x-user-id: $USER_ID" \
  -H "x-tenant-id: $TENANT_ID" \
  -d '{}' localhost:6001 NotificationService/GetUnreadCount
```

`x-service-id` hoặc `x-service-api-key` không hợp lệ cũng phải fail tương tự.

### Đăng ký browser FCM device

Dùng token thật do Firebase Web Messaging tạo. Nếu frontend đã đăng nhập, lấy
token từ request body của
`POST /api/v1/notifications/devices`:

```bash
export FCM_TOKEN='BROWSER_FCM_TOKEN'
```

```bash
notification_grpc NotificationService/RegisterDevice "{
  \"token\": \"$FCM_TOKEN\",
  \"platform\": \"Web\",
  \"appVersion\": \"local\"
}"
```

Gửi lại request để test nhánh cập nhật device đã tồn tại. Platform không hợp lệ
hoặc token có whitespace phải fail với `InvalidArgument`.

### Subscribe một shipment

```bash
export SHIPMENT_ID='SHIPMENT_UUID'
notification_grpc NotificationService/SubscribeShipment \
  "{\"shipmentId\":\"$SHIPMENT_ID\"}"
```

Gửi lại để xác nhận unique subscription không bị duplicate.

### Liệt kê và đếm notification

```bash
notification_grpc NotificationService/ListNotifications \
  '{"page":1,"pageSize":20,"unreadOnly":false}'

notification_grpc NotificationService/ListNotifications \
  '{"page":1,"pageSize":20,"unreadOnly":true}'

notification_grpc NotificationService/GetUnreadCount '{}'
```

### Đánh dấu notification đã đọc

```bash
export NOTIFICATION_ID='NOTIFICATION_UUID'
notification_grpc NotificationService/MarkNotificationRead \
  "{\"id\":\"$NOTIFICATION_ID\"}"

notification_grpc NotificationService/MarkAllNotificationsRead '{}'
```

## Shipment → Notification → popup FCM

Đây là luồng đơn giản nhất để test event đến popup thật. Frontend phải đang mở,
đã đăng nhập, được cấp quyền notification trên browser và có FCM listener đang
hoạt động. Device trong Notification phải dùng đúng FCM token của browser đó.

Tạo shipment:

```bash
shipment_grpc ShipmentWorkflowService/CreateShipment '{
  "orderId": "ORDER-NOTIFY-DEMO-001",
  "customerName": "VietLink",
  "originAddress": "Ho Chi Minh City",
  "destinationAddress": "Singapore",
  "originCountry": "VN",
  "destinationCountry": "SG",
  "cargoItems": [
    {
      "name": "Demo cargo",
      "quantity": 1,
      "weightKg": 10,
      "hsCode": "123456"
    }
  ]
}'
```

Lưu ID trả về rồi subscribe:

```bash
export SHIPMENT_ID='SHIPMENT_UUID'
notification_grpc NotificationService/SubscribeShipment \
  "{\"shipmentId\":\"$SHIPMENT_ID\"}"
```

Kích hoạt một status event:

```bash
shipment_grpc ShipmentWorkflowService/UpdateShipmentStatus "{
  \"id\": \"$SHIPMENT_ID\",
  \"status\": \"Submitted\",
  \"note\": \"Notification FCM smoke test\"
}"
```

Chờ 2–5 giây để Shipment outbox publisher và Notification consumer xử lý, sau
đó lấy notification đã lưu:

```bash
notification_grpc NotificationService/ListNotifications \
  '{"page":1,"pageSize":20,"unreadOnly":false}'
```

Kết quả mong đợi:

- có notification mới cho `SHIPMENT_ID`;
- `isRead` là `false`;
- log Notification có consumption và delivery;
- browser hiển thị popup FCM.

Chuỗi status hợp lệ để tạo thêm notification:

```text
Submitted → Planning → Negotiating → Confirmed → PickedUp
→ InTransit → Delivered → Completed
```

Mỗi lần chuyển status là một lần gọi `UpdateShipmentStatus` riêng.

## GPS → Notification → FCM

GPS `IngestPosition` không nhận `shipmentId`. GPS suy ra shipment từ
`VehicleShipmentAssignment` đang active, được projection từ `RouteAssignedEvent`.

Điều này dẫn đến:

1. Test GPS theo vehicle độc lập được ingest, current location, history,
   geofence và monitoring alert.
2. GPS alert không có shipment ID sẽ được xử lý là `NoRecipient`, nên không gửi
   popup.
3. Muốn test GPS alert → Notification → FCM, vehicle phải được assign vào
   shipment đã subscribe và cả hai record phải cùng tenant.
4. Contract gRPC hiện tại của Shipment Workflow chưa có RPC gán vehicle trực
   tiếp. Dùng assignment có sẵn hoặc xử lý `RouteAssignedEvent` đáng tin cậy
   trước khi test geofence.

Đây là cơ chế recipient resolution có chủ đích, không phải lỗi FCM.

## Test bằng Postman gRPC

1. Tạo một **gRPC Request**.
2. Dùng `grpc://localhost:6002` cho GPS hoặc `grpc://localhost:6001` cho
   Notification.
3. Import đúng file trong `protos/`.
4. Chọn service method và dán JSON body ở các phần test bên trên.
5. Thêm metadata headers trong phần Metadata gRPC dùng chung.

Notification bắt buộc có:

```text
x-service-id: staff-bff
x-service-api-key: local-notification-key
x-user-id: 01910000-0000-7000-8000-000000000001
x-tenant-id: 01920000-0000-7000-8000-000000000001
```

## Xử lý lỗi và dọn môi trường

| Hiện tượng | Cách xử lý |
| --- | --- |
| `Redis:Host is required` | Set `Redis__Host=localhost:6379` trước khi chạy GPS. |
| Connection refused trên `5434`/`5435` | Chạy Compose và kiểm tra port bị trùng. |
| Notification `Unauthenticated` | Kiểm tra service ID/key và metadata user/tenant. |
| Không có GPS current location | Ingest position cho đúng vehicle và tenant. |
| Có GPS alert nhưng không có popup | Kiểm tra vehicle assignment, shipment subscription và FCM token. |
| Không có Notification row | Chờ RabbitMQ/outbox xử lý và xem log service. |
| Browser không có popup | Kiểm tra permission, Firebase config, service worker, token và FE listener. |

Dừng hạ tầng nhưng không xóa volume local:

```bash
docker compose -f docker-compose.dev.yml down
```

Không thêm `-v` trừ khi bạn thực sự muốn xóa data local của PostgreSQL, Redis và
RabbitMQ.

## Kịch bản E2E đầy đủ: GPS → Notification → FCM → FE

Phần dưới đây là kịch bản chuẩn để copy và chạy lại từ đầu. Mỗi terminal có
biến môi trường riêng; mở terminal mới thì phải chạy lại toàn bộ export của
terminal đó.

### Terminal 1 — Hạ tầng local

~~~bash
cd /home/kaito/project/aurora-server
docker compose -f docker-compose.dev.yml up -d
docker compose -f docker-compose.dev.yml ps
~~~

Không chạy docker compose down -v trong lúc test vì lệnh đó xoá toàn bộ data
local của PostgreSQL, Redis và RabbitMQ.

### Terminal 2 — Notification Service

~~~bash
cd /home/kaito/project/aurora-server
export ASPNETCORE_ENVIRONMENT='Development'
export Redis__Host='localhost:6379'
export ServiceAuth__AllowedServiceId='staff-bff'
export ServiceAuth__ApiKey='local-notification-key'
export Firebase__Enabled='true'
export Firebase__CredentialsPath="$PWD/secrets/firebase/aurora-notification-admin.json"
dotnet run --project src/dotnet/Notification/Notification.csproj
~~~

Giữ terminal này để xem:

~~~text
Processed notification event ...
Notification delivery attempt ... with status Sent
~~~

Nếu thấy InvalidToken, provider_failure hoặc retry_exhausted thì Firebase Admin
đã nhận yêu cầu nhưng token/provider trả lỗi. Nếu không có log Processed
notification event thì event chưa tới Notification.

### Terminal 3 — Shipment Workflow

~~~bash
cd /home/kaito/project/aurora-server
export ASPNETCORE_ENVIRONMENT='Development'
export Redis__Host='localhost:6379'
dotnet run --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
~~~

### Terminal 4 — GPS Tracking

~~~bash
cd /home/kaito/project/aurora-server
export ASPNETCORE_ENVIRONMENT='Development'
export Redis__Host='localhost:6379'
export GpsMonitoring__SignalLossThreshold='00:00:20'
export GpsMonitoring__SignalLossScanInterval='00:00:05'
export GpsMonitoring__AbnormalStopDuration='00:00:20'
dotnet run --project src/dotnet/GpsTracking/GpsTracking.csproj
~~~

GPS không gửi notification cho mọi GpsPositionUpdatedEvent. Notification chỉ
consume GpsMonitoringAlertRaisedEvent như GeofenceEntered, GeofenceExited,
SignalLost và AbnormalStop.

### Terminal 5 — Migration, identity và gRPC helper

Chạy migration một lần:

~~~bash
cd /home/kaito/project/aurora-server
export ASPNETCORE_ENVIRONMENT='Development'
export Redis__Host='localhost:6379'

dotnet ef database update \
  --project src/dotnet/Notification/Notification.csproj \
  --startup-project src/dotnet/Notification/Notification.csproj

dotnet ef database update \
  --project src/dotnet/GpsTracking/GpsTracking.csproj \
  --startup-project src/dotnet/GpsTracking/GpsTracking.csproj
~~~

Sau đó vẫn trong Terminal 5, chạy toàn bộ block identity/helper này:

~~~bash
cd /home/kaito/project/aurora-server
export USER_ID='01910000-0000-7000-8000-000000000001'
export TENANT_ID='01920000-0000-7000-8000-000000000001'
export NOTIFICATION_KEY='local-notification-key'
export FCM_TOKEN=''
export SHIPMENT_ID=''
export VEHICLE_ID='vehicle-fcm-gps-demo-001'
export ROUTE_ID='route-fcm-gps-demo-001'
export GPS_ASSIGNMENT_ID='01a056f0-0000-7000-8000-000000000001'
export GPS_ASSIGNED_AT='2026-08-31T09:00:00Z'
export NOTIFICATION_ID=''
export GEOFENCE_ID=''

GPS_META=(
  -H "x-user-id: $USER_ID"
  -H "x-tenant-id: $TENANT_ID"
)

NOTIFICATION_META=(
  -H "x-service-id: staff-bff"
  -H "x-service-api-key: $NOTIFICATION_KEY"
  -H "x-user-id: $USER_ID"
  -H "x-tenant-id: $TENANT_ID"
)

gps_grpc() {
  grpcurl -plaintext \
    -import-path protos \
    -proto gps_tracking.proto \
    "${GPS_META[@]}" \
    -d "$2" localhost:6002 "$1"
}

notification_grpc() {
  grpcurl -plaintext \
    -import-path protos \
    -proto notification.proto \
    "${NOTIFICATION_META[@]}" \
    -d "$2" localhost:6001 "$1"
}

shipment_grpc() {
  grpcurl -plaintext \
    -import-path protos \
    -proto shipment_workflow.proto \
    "${GPS_META[@]}" \
    -d "$2" localhost:6000 "$1"
}
~~~

Mở terminal mới để test thì phải chạy lại nguyên block identity/helper.

### Terminal 6 — Frontend

~~~bash
cd /home/kaito/project/aurora-client
pnpm dev
~~~

Mở http://localhost:3000/notifications, đăng nhập đúng user/tenant, cấp quyền
notification và bấm Enable browser notifications. Nếu dùng token để test gRPC,
copy token từ FE rồi chạy trong Terminal 5:

~~~bash
export FCM_TOKEN='PASTE_TOKEN_COPIED_FROM_BROWSER'

notification_grpc NotificationService/RegisterDevice "{
  \"token\": \"$FCM_TOKEN\",
  \"platform\": \"Web\",
  \"appVersion\": \"local\"
}"
~~~

Response có id, platform Web và isActive true nghĩa là token đã được lưu. Lệnh
này chưa gửi message, nên Firebase Console không hiển thị gì là đúng.

### Bước 1 — Tạo shipment và subscribe

~~~bash
shipment_grpc ShipmentWorkflowService/CreateShipment '{
  "orderId": "ORDER-FCM-DEMO-001",
  "customerName": "VietLink",
  "originAddress": "Ho Chi Minh City",
  "destinationAddress": "Singapore",
  "originCountry": "VN",
  "destinationCountry": "SG",
  "cargoItems": [
    {
      "name": "FCM demo cargo",
      "quantity": 1,
      "weightKg": 10,
      "hsCode": "123456"
    }
  ]
}'
~~~

Copy id trong response:

~~~bash
export SHIPMENT_ID='SHIPMENT_ID_FROM_CREATE_RESPONSE'

notification_grpc NotificationService/SubscribeShipment "{
  \"shipmentId\": \"$SHIPMENT_ID\"
}"
~~~

### Bước 2 — Test Shipment → Notification → FCM

Giữ FE mở rồi phát status event:

~~~bash
shipment_grpc ShipmentWorkflowService/UpdateShipmentStatus "{
  \"id\": \"$SHIPMENT_ID\",
  \"status\": \"Submitted\",
  \"note\": \"Shipment to FCM popup smoke test\"
}"
~~~

Chờ 2–5 giây:

~~~bash
notification_grpc NotificationService/ListNotifications '{
  "page": 1,
  "pageSize": 20,
  "unreadOnly": false
}'

notification_grpc NotificationService/GetUnreadCount '{}'
~~~

Kỳ vọng: có notification mới, unread count bằng 1, log Notification có
delivery status Sent và FE hiện popup. Nếu gọi MarkNotificationRead thành công,
GetUnreadCount có thể trả {} vì count bằng 0 và grpcurl ẩn field protobuf mặc
định.

### Bước 3 — Tạo assignment local cho luồng GPS

GPS nhận assignment từ RouteAssignedEvent, nhưng proto hiện tại chưa có RPC
assign vehicle trực tiếp. SQL dưới đây chỉ là workaround local để liên kết
vehicle với shipment; không dùng trong production:

~~~bash
docker exec aurora-gps-postgres psql \
  -U postgres \
  -d aurora_gps_tracking \
  -c "INSERT INTO vehicle_shipment_assignments
      (\"Id\", \"ShipmentId\", \"RouteId\", \"VehicleId\", \"AssignedAt\", \"EndedAt\", \"CreatedAt\", \"UpdatedAt\", \"CreatedBy\", \"UpdatedBy\", \"TenantId\")
      VALUES ('$GPS_ASSIGNMENT_ID', '$SHIPMENT_ID', '$ROUTE_ID', '$VEHICLE_ID', '$GPS_ASSIGNED_AT', NULL, '$GPS_ASSIGNED_AT', NULL, '$USER_ID', NULL, '$TENANT_ID')
      ON CONFLICT (\"Id\") DO NOTHING;"
~~~

Kiểm tra:

~~~bash
docker exec aurora-gps-postgres psql \
  -U postgres \
  -d aurora_gps_tracking \
  -c 'SELECT "ShipmentId", "RouteId", "VehicleId", "AssignedAt", "EndedAt", "TenantId" FROM vehicle_shipment_assignments WHERE "EndedAt" IS NULL ORDER BY "AssignedAt" DESC;'
~~~

### Bước 4 — Test GPS → Notification → FCM bằng GeofenceEntered

Tạo geofence quanh vị trí sẽ gửi:

~~~bash
gps_grpc GpsTrackingService/CreateGeofence "{
  \"name\": \"FCM Demo Geofence\",
  \"latitude\": 10.7769,
  \"longitude\": 106.7009,
  \"radiusMeters\": 100,
  \"shipmentId\": \"$SHIPMENT_ID\",
  \"vehicleId\": \"$VEHICLE_ID\"
}"
~~~

Gửi vị trí đầu tiên vào geofence:

~~~bash
gps_grpc GpsTrackingService/IngestPosition "{
  \"externalReadingId\": \"fcm-gps-reading-inside-001\",
  \"deviceId\": \"gps-device-fcm-demo-001\",
  \"vehicleId\": \"$VEHICLE_ID\",
  \"latitude\": 10.7769,
  \"longitude\": 106.7009,
  \"speedKph\": 20,
  \"headingDegrees\": 90,
  \"accuracyMeters\": 5,
  \"recordedAt\": \"2026-08-31T09:01:00Z\"
}"
~~~

Kiểm tra GPS alert:

~~~bash
gps_grpc GpsTrackingService/ListMonitoringAlerts '{
  "alertType": "GeofenceEntered",
  "status": "Active",
  "page": 1,
  "pageSize": 20
}'
~~~

Chờ 2–5 giây và kiểm tra Notification:

~~~bash
notification_grpc NotificationService/ListNotifications '{
  "page": 1,
  "pageSize": 20,
  "unreadOnly": false
}'
~~~

Kỳ vọng có notification type GPS_MONITORING_ALERT_RAISED và log delivery Sent.
FE sẽ hiện popup nếu đang foreground; nếu tab background thì browser hiện
notification hệ thống.

### Bước 5 — Test SignalLost

Sau khi đã có assignment và current location, không gửi vị trí quá 20 giây rồi
chờ scan interval 5 giây:

~~~bash
gps_grpc GpsTrackingService/ListMonitoringAlerts '{
  "alertType": "SignalLost",
  "status": "Active",
  "page": 1,
  "pageSize": 20
}'
~~~

Nếu có alert, kiểm tra Notification list và log delivery. Gửi lại vị trí cho
cùng vehicle sẽ resolve alert cũ và không tạo duplicate notification.

### Vì sao Firebase Console không thấy gì?

RegisterDevice chỉ lưu browser token. SubscribeShipment chỉ lưu recipient
subscription. Firebase Console không hiển thị log cho hai lệnh này và cũng
không phải log viewer cho từng message do Firebase Admin SDK local gửi.

Muốn xác nhận:

1. Notification terminal có log Processed notification event.
2. Notification terminal có delivery status Sent.
3. Notification ListNotifications có row mới.
4. Browser FE hiện popup.

Nếu status là Sent nhưng không có popup, kiểm tra permission, service worker và
tab đang foreground/background. Nếu không có status Sent, kiểm tra Firebase
Admin credential, token, RabbitMQ và recipient subscription.
