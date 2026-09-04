# .NET Notification Deployment & Environment Configuration

Primary and active V1 Notification service implementation with gRPC (`notification.proto` port 5000), MassTransit event consumers, SMTP dispatch, In-App notification tracking, and Firebase Cloud Messaging (FCM).

## Environment Variable Matrix

| Variable | Required | Secret | Local Source | AKS Source | Default |
|---|:---:|:---:|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `.env.local` | ConfigMap | `Production` |
| `ConnectionStrings__NotificationDatabase` | Yes | Yes | Local `.env` | Key Vault (`notification-db-connection`) | Neon Managed PostgreSQL |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Local `.env` | Key Vault (`notification-db-connection`) | Neon Managed PostgreSQL |
| `RabbitMQ__Host` | Yes | No | `.env.local` | ConfigMap | `rabbitmq.aurora.svc.cluster.local` |
| `RabbitMQ__Port` | Yes | No | `.env.local` | ConfigMap | `5672` |
| `RabbitMQ__Username` | Yes | No | `.env.local` | ConfigMap | `aurora_admin` |
| `RabbitMQ__Password` | Yes | Yes | Local `.env` | Key Vault (`rabbitmq-password`) | — |
| `Smtp__Host` | Yes | No | `.env.local` | ConfigMap | `stalwart-ingress.aurora.svc.cluster.local` |
| `Smtp__Port` | Yes | No | `.env.local` | ConfigMap | `25` |
| `Smtp__Password` | Yes | Yes | Local `.env` | Key Vault (`smtp-password`) | — |
| `ServiceAuth__AllowedServiceId` | Yes | No | `.env.local` | ConfigMap | `staff-bff` |
| `ServiceAuth__ApiKey` | Yes | Yes | Local `.env` | Key Vault (`notification-service-api-key`) | — |
| `Firebase__Enabled` | Yes | No | `.env.local` | ConfigMap | `true` |
| `Firebase__CredentialsPath` | Yes | No | Local `.env` | ConfigMap | `/app/secrets/firebase/aurora-notification-admin.json` |

## Firebase Secret & Volume Mount Architecture

1. **Azure Key Vault**:
   * Secret Name: `notification-firebase-admin-json`
   * Value: Toàn bộ nội dung raw JSON của Firebase Service Account (`aurora-notification-admin.json`).
   * Command tạo secret:
     ```bash
     az keyvault secret set \
       --vault-name kv-aurora-shared-demo \
       --name notification-firebase-admin-json \
       --file ./secrets/firebase/aurora-notification-admin.json \
       --encoding utf-8
     ```

2. **External Secrets Operator (ESO)**:
   * Pulls `notification-firebase-admin-json` từ Azure Key Vault.
   * Tạo Kubernetes Secret: `notification-kv-secret` với key `aurora-notification-admin.json`.

3. **Pod Volume Mount**:
   * Kubernetes Secret `notification-kv-secret` được mount vào container:
     * Mount Path: `/app/secrets/firebase`
     * Target File: `/app/secrets/firebase/aurora-notification-admin.json`
     * Read-only: `true`

4. **Application Runtime**:
   * `FirebaseOptions:CredentialsPath` trỏ đến `/app/secrets/firebase/aurora-notification-admin.json`.
   * `FirebasePushProvider` và `FirebaseAdminInitializer` tự động đọc file JSON này qua `GoogleCredential.FromFile(...)`.
