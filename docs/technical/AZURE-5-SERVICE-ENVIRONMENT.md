# Environment cho 5 service Aurora

Áp dụng riêng cho `ShipmentWorkflow`, `Notification`, `GpsTracking`,
`DocumentOcr` và `RegulatoryCompliance`.

## 1. Tên Secret Azure và tên environment variable

Azure Secret name không được chứa `_`, vì vậy chỉ đổi **tên Secret**:

```text
POSTGRES_DB -> POSTGRES-DB
```

Environment variable được inject vào container vẫn phải giữ format ứng dụng
đang đọc. Không sửa source để đọc tên có dấu `-`.

```text
Secret name trên Azure: SHIPMENT-DB-CONNECTION
Environment variable:   ConnectionStrings__DefaultConnection
Secret value:            Host=...;Port=5432;Database=...;Username=...;Password=...
```

Với .NET, dấu `__` ánh xạ vào section configuration:

```text
Redis__Host                 -> Redis:Host
RabbitMQ__Password          -> RabbitMQ:Password
Firebase__CredentialsPath   -> Firebase:CredentialsPath
Grpc__AiGovernance__Url     -> Grpc:AiGovernance:Url
```

Luồng là:

```text
Azure Secret (dấu -) -> secretRef/mapping -> env trong container (dấu __/_)
                     -> ASP.NET Core IConfiguration -> service
```

### 1.1. Quy ước tên Secret và nơi gắn

Secret name là tên hiển thị trong Azure Key Vault. Environment variable là
tên được inject vào từng container; hai tên này không cần giống nhau.

| Secret name trong Key Vault | Gắn vào service | Environment variable trong container |
|---|---|---|
| `SHIPMENT-DB-CONNECTION` | ShipmentWorkflow | `ConnectionStrings__DefaultConnection` |
| `NOTIFICATION-DB-CONNECTION` | Notification | `ConnectionStrings__NotificationDatabase` |
| `GPS-DB-CONNECTION` | GpsTracking | `ConnectionStrings__DefaultConnection` |
| `DOCUMENT-OCR-DB-CONNECTION` | DocumentOcr | `ConnectionStrings__DefaultConnection` |
| `REGULATORY-COMPLIANCE-DB-CONNECTION` | RegulatoryCompliance | `ConnectionStrings__DefaultConnection` |
| `REDIS-PASSWORD` | Cả 5 service | `Redis__Password` |
| `RABBITMQ-PASSWORD` | Cả 5 service | `RabbitMQ__Password` |
| `NOTIFICATION-SERVICE-API-KEY` | Notification và Staff BFF | Xem mục 4 và 8 |
| `NOTIFICATION-FIREBASE-ADMIN-JSON` | Chỉ Notification | Mount thành file, xem mục 4 |

Có thể dùng tên khác, ví dụ `DATABASEURL`, nhưng phải map đúng vào biến của
service. Một Secret `DATABASEURL` duy nhất không thay thế được 5 connection
string riêng của 5 service.

### 1.2. Điền Name và Secret value trên Azure

Trong màn hình **Key Vault → Secrets → Generate/Import → Manual**:

```text
Name:          NOTIFICATION-SERVICE-API-KEY
Secret value:  một chuỗi ngẫu nhiên dài, tự tạo
```

Giá trị của `NOTIFICATION-SERVICE-API-KEY` không phải Firebase `private_key`.
Firebase `private_key` nằm bên trong giá trị JSON của
`NOTIFICATION-FIREBASE-ADMIN-JSON`.

Với mỗi database, Secret value là connection string thật của database tương
ứng, ví dụ dạng Npgsql:

```text
Host=<neon-host>;Port=5432;Database=<database-name>;Username=<username>;Password=<password>;SSL Mode=Require
```

Không đặt dấu nháy đơn quanh giá trị khi dán vào Azure. Không ghi password
thật vào tài liệu hoặc Git.

## 2. Biến dùng chung

Đây là tên environment variable **trong container**:

```text
ASPNETCORE_ENVIRONMENT=Production
Redis__Host=<azure-redis-host>:6379
Redis__Password=<azure-redis-password>
RabbitMQ__Host=<azure-rabbitmq-host>
RabbitMQ__Username=<azure-rabbitmq-username>
RabbitMQ__Password=<azure-rabbitmq-password>
```

Secret name tương ứng:

```text
REDIS-HOST
REDIS-PASSWORD
RABBITMQ-HOST
RABBITMQ-USERNAME
RABBITMQ-PASSWORD
```

Mỗi service cần được map các Secret dùng chung này vào đúng environment
variable. Giá trị `<...>` phải thay bằng giá trị thật của Azure.

## 3. ShipmentWorkflow

Environment variable:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<secret-reference-SHIPMENT-DB-CONNECTION>
Redis__Host=<azure-redis-host>:6379
Redis__Password=<azure-redis-password>
RabbitMQ__Host=<azure-rabbitmq-host>
RabbitMQ__Username=<azure-rabbitmq-username>
RabbitMQ__Password=<azure-rabbitmq-password>
ShipmentOutbox__BatchSize=50
ShipmentOutbox__MaxRetries=5
ShipmentOutbox__PollingInterval=00:00:02
```

Secret name:

```text
SHIPMENT-DB-CONNECTION
REDIS-HOST
REDIS-PASSWORD
RABBITMQ-HOST
RABBITMQ-USERNAME
RABBITMQ-PASSWORD
```

Map `SHIPMENT-DB-CONNECTION` vào `ConnectionStrings__DefaultConnection`.

## 4. Notification

Environment variable:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__NotificationDatabase=<secret-reference-NOTIFICATION-DB-CONNECTION>
Redis__Host=<azure-redis-host>:6379
Redis__Password=<azure-redis-password>
RabbitMQ__Host=<azure-rabbitmq-host>
RabbitMQ__Username=<azure-rabbitmq-username>
RabbitMQ__Password=<azure-rabbitmq-password>
ServiceAuth__AllowedServiceId=staff-bff
ServiceAuth__ApiKey=<secret-reference-NOTIFICATION-SERVICE-API-KEY>
Firebase__Enabled=true
Firebase__CredentialsPath=/app/secrets/firebase/aurora-notification-admin.json
```

Secret name:

```text
NOTIFICATION-DB-CONNECTION
NOTIFICATION-SERVICE-API-KEY
NOTIFICATION-FIREBASE-ADMIN-JSON
REDIS-HOST
REDIS-PASSWORD
RABBITMQ-HOST
RABBITMQ-USERNAME
RABBITMQ-PASSWORD
```

Map:

```text
NOTIFICATION-DB-CONNECTION      -> ConnectionStrings__NotificationDatabase
NOTIFICATION-SERVICE-API-KEY     -> ServiceAuth__ApiKey
```

`NOTIFICATION-FIREBASE-ADMIN-JSON` phải được mount thành file:

```text
/app/secrets/firebase/aurora-notification-admin.json
```

`Firebase__CredentialsPath` là đường dẫn **bên trong container**. Không dùng
đường dẫn `/home/kaito/...` trên Azure và không commit service-account JSON.
Các field `project_id`, `private_key`, `client_email` được Firebase Admin SDK
đọc trực tiếp từ JSON; không cần tách thành biến riêng khi dùng path.

`NOTIFICATION-SERVICE-API-KEY` là secret giao tiếp nội bộ. Notification đọc
giá trị này qua `ServiceAuth__ApiKey`; Staff BFF phải đọc cùng một giá trị qua
`Grpc__Notification__ServiceApiKey`. Chỉ tạo một Secret trong Key Vault rồi
reference nó ở hai application, không tạo hai giá trị khác nhau.

Khi chạy local:

```text
Firebase__Enabled=true
Firebase__CredentialsPath=/home/kaito/project/aurora-server/secrets/firebase/aurora-notification-admin.json
```

## 5. GpsTracking

Environment variable:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<secret-reference-GPS-DB-CONNECTION>
Redis__Host=<azure-redis-host>:6379
Redis__Password=<azure-redis-password>
RabbitMQ__Host=<azure-rabbitmq-host>
RabbitMQ__Username=<azure-rabbitmq-username>
RabbitMQ__Password=<azure-rabbitmq-password>
GpsMonitoring__StationarySpeedKph=1
GpsMonitoring__SignalLossThreshold=00:05:00
GpsMonitoring__SignalLossScanInterval=00:01:00
GpsMonitoring__AbnormalStopDuration=00:05:00
GpsMonitoring__SignalLossBatchSize=100
GpsOutbox__BatchSize=50
GpsOutbox__MaxRetries=5
GpsOutbox__PollingInterval=00:00:02
```

Secret name:

```text
GPS-DB-CONNECTION
REDIS-HOST
REDIS-PASSWORD
RABBITMQ-HOST
RABBITMQ-USERNAME
RABBITMQ-PASSWORD
```

Map `GPS-DB-CONNECTION` vào `ConnectionStrings__DefaultConnection`.

Local có thể dùng ngưỡng ngắn để test:

```text
GpsMonitoring__SignalLossThreshold=00:00:20
GpsMonitoring__SignalLossScanInterval=00:00:05
GpsMonitoring__AbnormalStopDuration=00:00:20
```

Không dùng ngưỡng 20 giây trên Production nếu chưa được nghiệp vụ phê duyệt.

## 6. DocumentOcr

Environment variable:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<secret-reference-DOCUMENT-OCR-DB-CONNECTION>
Redis__Host=<azure-redis-host>:6379
Redis__Password=<azure-redis-password>
RabbitMQ__Host=<azure-rabbitmq-host>
RabbitMQ__Username=<azure-rabbitmq-username>
RabbitMQ__Password=<azure-rabbitmq-password>
Grpc__AiGovernance__Url=<azure-ai-governance-url>
DocumentProcessing__Provider=Deterministic
DocumentProcessing__MaxDocumentBytes=10485760
DocumentProcessing__MaxPages=50
DocumentProcessing__ReviewConfidenceThreshold=0.85
DocumentProcessing__SupportedMimeTypes__0=application/pdf
DocumentProcessing__SupportedMimeTypes__1=image/jpeg
DocumentProcessing__SupportedMimeTypes__2=image/png
DocumentProcessing__SupportedMimeTypes__3=image/tiff
DocumentOcrWorker__BatchSize=10
DocumentOcrWorker__MaxAttempts=3
DocumentOcrWorker__PollingInterval=00:00:02
DocumentOcrWorker__LeaseDuration=00:02:00
DocumentOcrWorker__HeartbeatInterval=00:00:30
DocumentOcrWorker__BaseRetryDelay=00:00:10
DocumentOcrWorker__MaxRetryDelay=00:05:00
DocumentOcrWorker__MaxRetryJitter=00:00:05
DocumentOcrOutbox__BatchSize=50
DocumentOcrOutbox__MaxRetries=5
DocumentOcrOutbox__PollingInterval=00:00:02
```

Secret name:

```text
DOCUMENT-OCR-DB-CONNECTION
AI-GOVERNANCE-URL
REDIS-HOST
REDIS-PASSWORD
RABBITMQ-HOST
RABBITMQ-USERNAME
RABBITMQ-PASSWORD
```

Map `DOCUMENT-OCR-DB-CONNECTION` vào `ConnectionStrings__DefaultConnection`.
Nếu dùng AI thật, map `AI-GOVERNANCE-URL` vào `Grpc__AiGovernance__Url`.

Local có thể dùng:

```text
DocumentProcessing__Provider=Deterministic
Grpc__AiGovernance__Url=http://localhost:9090
```

`Deterministic` chỉ tạo kết quả mô phỏng, không phải OCR AI thật.

## 7. RegulatoryCompliance

Environment variable:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<secret-reference-REGULATORY-COMPLIANCE-DB-CONNECTION>
Redis__Host=<azure-redis-host>:6379
Redis__Password=<azure-redis-password>
RabbitMQ__Host=<azure-rabbitmq-host>
RabbitMQ__Username=<azure-rabbitmq-username>
RabbitMQ__Password=<azure-rabbitmq-password>
Grpc__AiGovernance__Url=<azure-ai-governance-url>
RegulatoryCompliance__EmbeddingProvider=Deterministic
RegulatoryCompliance__EmbeddingModel=deterministic-local
RegulatoryCompliance__EmbeddingModelVersion=1
RegulatoryCompliance__EmbeddingDimension=64
RegulatoryCompliance__EmbeddingBatchSize=64
RegulatoryCompliance__EmbeddingPollingInterval=00:00:02
RegulatoryCompliance__ProviderTimeout=00:00:30
RegulatoryCompliance__RetrievalMaximumTopK=20
RegulatoryCompliance__RetrievalMinimumScore=0.2
RegulatoryCompliance__OutboxBatchSize=50
RegulatoryCompliance__OutboxMaxRetries=5
RegulatoryCompliance__OutboxPollingInterval=00:00:02
```

Secret name:

```text
REGULATORY-COMPLIANCE-DB-CONNECTION
AI-GOVERNANCE-URL
REDIS-HOST
REDIS-PASSWORD
RABBITMQ-HOST
RABBITMQ-USERNAME
RABBITMQ-PASSWORD
```

Map `REGULATORY-COMPLIANCE-DB-CONNECTION` vào
`ConnectionStrings__DefaultConnection` và `AI-GOVERNANCE-URL` vào
`Grpc__AiGovernance__Url`.

Database phải có cấu hình extension/vector phù hợp với schema Regulatory
Compliance nếu service dùng pgvector.

Local có thể dùng:

```text
RegulatoryCompliance__Provider=Deterministic
RegulatoryCompliance__Model=deterministic-v1
Grpc__AiGovernance__Url=http://localhost:9090
```

## 8. Staff BFF gọi Notification

Staff BFF không phải một trong 5 service backend ở phạm vi tài liệu này,
nhưng bắt buộc có cấu hình caller nếu muốn chạy luồng:

```text
FE -> Staff BFF -> Notification (gRPC) -> Firebase FCM
```

Trong application settings/environment của **Staff BFF**:

```text
Grpc__Notification__Url=<internal-url-cua-notification>
Grpc__Notification__ServiceApiKey=<reference-NOTIFICATION-SERVICE-API-KEY>
```

Map như sau:

```text
Grpc__Notification__ServiceApiKey
    -> secret reference NOTIFICATION-SERVICE-API-KEY

Grpc__Notification__Url
    -> URL nội bộ của Notification, không phải secret
```

Notification phải có:

```text
ServiceAuth__AllowedServiceId=staff-bff
ServiceAuth__ApiKey=<reference-NOTIFICATION-SERVICE-API-KEY>
```

BFF đã có code interceptor gửi `x-service-id=staff-bff` và
`x-service-api-key`. Khi chỉ implement Notification, không cần sửa code BFF;
chỉ cần người deploy gắn đúng hai biến runtime trên BFF.

## 9. Cách map Secret vào Container App

Ở mỗi Container App, vào **Environment variables → Add**:

1. Với biến thường, chọn **Value** và nhập giá trị không nhạy cảm, ví dụ
   `ASPNETCORE_ENVIRONMENT=Production`.
2. Với password, connection string hoặc API key, chọn **Secret reference**
   và chọn Secret tương ứng trong Container App/Key Vault.
3. Tên biến bên trái phải là tên .NET có `__`, ví dụ
   `ConnectionStrings__DefaultConnection`, không phải tên Secret có dấu `-`.
4. Với Firebase JSON, lưu toàn bộ JSON vào
   `NOTIFICATION-FIREBASE-ADMIN-JSON`, mount thành file trong container rồi
   đặt `Firebase__CredentialsPath` tới đường dẫn file đó.

Các biến bắt buộc tối thiểu khi deploy Notification:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__NotificationDatabase=<secret reference>
Redis__Host=<redis-host>
RabbitMQ__Host=<rabbitmq-host>
RabbitMQ__Username=<rabbitmq-username>
RabbitMQ__Password=<secret reference>
ServiceAuth__AllowedServiceId=staff-bff
ServiceAuth__ApiKey=<secret reference>
Firebase__Enabled=true
Firebase__CredentialsPath=/app/secrets/firebase/aurora-notification-admin.json
```

Các biến bắt buộc tối thiểu trên Staff BFF để gọi Notification:

```text
Grpc__Notification__Url=<notification-internal-url>
Grpc__Notification__ServiceApiKey=<same-secret-reference>
```

## 10. PostgreSQL và `POSTGRES_*`

Nếu dùng image PostgreSQL chính thức, environment variable của **database
container** phải giữ nguyên tên chuẩn:

```text
POSTGRES_DB
POSTGRES_USER
POSTGRES_PASSWORD
```

Azure Secret name có thể là:

```text
POSTGRES-DB
POSTGRES-USER
POSTGRES-PASSWORD
```

nhưng secret mapping phải inject thành `POSTGRES_DB`, `POSTGRES_USER` và
`POSTGRES_PASSWORD` trong database container. Năm application service không
đọc trực tiếp `POSTGRES_DB`; chúng đọc connection string.

## 11. Không đưa lên Azure Production

Không đưa các giá trị sau lên Production:

```text
localhost
127.0.0.1
Port=5433, 5434, 5435, 5436 hoặc 5437
DevelopmentIdentity__*
/home/kaito/...
```

Không commit `.env`, `.env.local`, connection string có password thật hoặc
Firebase service-account JSON. Các biến `NEXT_PUBLIC_FIREBASE_*` thuộc FE,
không thuộc 5 backend service này.

## 12. Checklist Azure

- [ ] Đã tạo Secret name không có `_`, chỉ dùng chữ, số và `-`.
- [ ] Đã map từng Secret vào đúng environment variable có `__` của .NET.
- [ ] Mỗi service có database connection string riêng.
- [ ] Mỗi service có Redis và RabbitMQ config.
- [ ] Notification có Firebase enabled và đã mount JSON đúng path.
- [ ] Đã bỏ `DevelopmentIdentity__*` khỏi Production.
- [ ] GPS dùng ngưỡng cảnh báo Production phù hợp.
- [ ] DocumentOcr và RegulatoryCompliance có AI Governance URL thật nếu cần AI thật.
- [ ] Đã kiểm tra health/readiness và log kết nối của từng service.
