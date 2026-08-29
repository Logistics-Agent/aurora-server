# Multi-Channel Notification & Alerting Service — Service Overview

> **Service Layer**: Event-Driven Alerting, Multi-Channel Delivery & Preferences  
> **Target Audience**: Technical Recruiters, Distributed Systems Engineers, System Architects  
> **Source-of-Truth**: `src/dotnet/Notification`, `NotificationMessage.cs`, `NotificationPreference.cs`, `NotificationDeliveryAttempt.cs`, `protos/notification.proto`.

---

## 1. Service Purpose & Problem Solved

Logistics stakeholders require immediate updates regarding shipment departures, customs holds, geofence breaches, and invoice due dates across multiple communication channels (In-App UI, Email, SMS, Webhooks). Without a centralized notification hub, individual microservices end up hardcoding email/SMS integrations, leading to duplicate notifications, ignored user channel preferences, and missing delivery audit trails.

The **Notification Service** provides **Event-Driven Multi-Channel Dispatch + User Preference Routing + Idempotent Delivery**:
- **Multi-Channel Hub**: Dispatches alerts across **In-App WebSockets**, **Email** (via `MailService`), **SMS**, and **Tenant Webhooks**.
- **User & Tenant Preference Engine**: Respects granular opt-in/opt-out preferences per notification category (`ShipmentMilestones`, `BillingInvoices`, `SecurityAlerts`, `RouteExceptions`).
- **Idempotent Ingestion**: Tracks consumed message IDs (`ConsumedIntegrationEvent`) to guarantee that duplicate RabbitMQ deliveries never result in duplicate SMS or email alerts to customers.
- **Delivery Audit & Retry**: Logs every transmission attempt with latency, response codes, and exponential backoff retry.

---

## 2. Architecture & Tech Stack

```
[ Domain Microservices (Shipment, Billing, GPS, Mail) ]
                          │
                          ▼ (RabbitMQ Integration Events)
┌─────────────────────────────────────────────────────────────┐
│                 Notification Microservice (.NET 10)         │
│  ├── Idempotent Event Consumer (ConsumedIntegrationEvent)   │
│  ├── Template & Localization Engine                         │
│  ├── User Preference Filter (NotificationPreference)        │
│  ├── Multi-Channel Router (InApp, Email, SMS, Webhook)      │
│  └── Delivery Attempt Logger & Retry Pipeline               │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]        [ Delivery Providers ]
    (Messages, Preferences, Logs)     ├── RealtimeHub (WebSockets)
                                      ├── MailService (Email)
                                      └── SMS / External Webhooks
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Persistence & ORM** | Entity Framework Core 10, PostgreSQL 16 (Neon Serverless SSL) |
| **Event Broker & Consumer**| RabbitMQ, MassTransit |
| **BFF Client** | `Staff.Bff` (`/api/v1/notifications/*`), `Admin.Bff` |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`NotificationMessages`**: Tracks `TenantId`, `RecipientUserId`, `Category`, `Title`, `Body`, `Channel`, `Status` (`Queued`, `Sent`, `Failed`, `Read`), `DeliveredAt`.
- **`NotificationPreferences`**: User and tenant configuration flags for each channel and category.
- **`NotificationDeliveryAttempts`**: Detailed log of provider HTTP/SMTP responses, retry counts, latency, and error strings.
- **`ConsumedIntegrationEvents`**: Idempotency ledger storing processed event UUIDs and timestamps.

---

## 4. API & Contract Surface

Exposed via `protos/notification.proto` (`NotificationService`):
- `SendNotification`: Synchronous gRPC dispatch for high-priority operational alerts.
- `GetNotifications`: Paginated in-app notification inbox with unread count.
- `MarkAsRead`: Updates notification status to `Read`.
- `UpdatePreferences`: Configures channel enable/disable settings per category.

---

## 5. Security & Invariants

1. **Idempotency Barrier**: Every consumed integration event verifies `event_id` in `ConsumedIntegrationEvents`; duplicate events are acknowledged and discarded without re-sending.
2. **Preference Invariant**: Non-critical marketing/operational notifications are blocked if the user has disabled the channel; critical security/quarantine alerts bypass suppression.
3. **Current Maturity**: Production-ready multi-channel dispatcher with complete idempotency and delivery logging.
