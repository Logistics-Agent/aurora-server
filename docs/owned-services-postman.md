# Testing Owned gRPC Services with Postman

This setup is for local development only. Each service keeps tenant isolation internally, but
requests without identity metadata use the fixed identity configured in
`appsettings.Development.json`.

## Local endpoints

| Service | gRPC URL | PostgreSQL database | PostgreSQL port |
| --- | --- | --- | --- |
| Shipment Workflow | `http://localhost:6000` | `aurora_shipment_workflow` | `5433` |
| Notification | `http://localhost:6001` | `aurora_notification` | `5434` |
| GPS Tracking | `http://localhost:6002` | `aurora_gps_tracking` | `5435` |
| Document OCR | `http://localhost:6003` | `aurora_document_ocr` | `5436` |
| Regulatory Compliance | `http://localhost:6004` | `aurora_regulatory_compliance` | `5437` |

Redis remains on `6379`, RabbitMQ on `5672`, and RabbitMQ Management on `15672`.

## Start locally

Start the shared local infrastructure:

```bash
docker compose -f docker-compose.dev.yml up -d
```

Run the service being tested, for example:

```bash
dotnet run --project src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj --launch-profile http
```

Use the same command with the appropriate project for the other services.

## Postman gRPC request

1. Create a gRPC request in Postman.
2. Enter the service URL from the table above.
3. Import the matching file from `protos/`: `shipment_workflow.proto`, `notification.proto`,
   `gps_tracking.proto`, `document_ocr.proto`, or `regulatory_compliance.proto`.
4. Select an RPC, provide its protobuf message as JSON, and invoke it.
5. Leave metadata empty for the configured local development identity.

Postman does not need `x-tenant-id` or `x-user-id` while the process runs with
`ASPNETCORE_ENVIRONMENT=Development` and `DevelopmentIdentity:Enabled=true`.

When testing a specific identity, send both headers:

```text
x-user-id: <valid-guid>
x-tenant-id: <valid-guid>
```

Explicit identity metadata overrides the local identity. A partial or malformed identity does not
trigger the fallback and should be rejected by tenant-protected operations.

## Security boundary

The fallback is ignored outside the `Development` environment, even if an enabled section is
present. Production and integration environments must receive trusted identity metadata from the
Gateway/IAM path. Client requests never supply permissions; the only local permissions configured
are the two Regulatory Compliance ingestion permissions.
