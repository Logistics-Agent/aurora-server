# Multi-Channel Notification & Alerting Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & Distributed Systems Interviewers  
> **Source-of-Truth**: Grounded 100% in .NET 10 `Notification` implementation.

---

### Q1 (Junior): How does the notification service prevent sending duplicate emails when RabbitMQ redelivers an event?
**Answer**:  
The service implements an **Idempotency Consumer Pattern** using the table `consumed_integration_events`. When an event arrives, the consumer checks whether the broker's unique `MessageId` exists in the database. If it exists, the consumer immediately acknowledges and discards the duplicate message without sending an email. If absent, it processes the notification and records the `MessageId` in the same database transaction.

---

### Q2 (Mid): How are user notification preferences enforced?
**Answer**:  
Before dispatching a notification, the service queries `NotificationPreference` using `(tenantId, userId)`. It verifies whether the recipient has enabled the target channel (e.g. Email, SMS, In-App) for that specific notification category (e.g. `BillingInvoices`, `ShipmentMilestones`). If disabled, the notification is suppressed, saving SMS costs and avoiding customer spam.

---

### Q3 (Mid): How does the in-app notification channel communicate with the user's browser?
**Answer**:  
When an in-app notification is persisted, the service publishes a lightweight real-time event to RabbitMQ (`notification.inapp.created`). The `RealtimeHub` (Socket.IO / WebSocket service) consumes this event and pushes the notification directly to the connected user's browser session, updating the notification badge in real time.

---

### Q4 (Senior): What happens if an external notification channel (e.g. SMS provider) is experiencing an outage?
**Answer**:  
Outbound channel delivery is isolated from inbound event ingestion:
1. Notifications are persisted in `Status = Queued`.
2. Delivery workers execute HTTP requests with exponential backoff (e.g. 10s, 20s, 40s).
3. Failed attempts are recorded in `NotificationDeliveryAttempt` with provider error payloads.
4. If max retries are exceeded, status transitions to `Failed`, alerting operations without blocking other channels (Email and In-App delivery continue unaffected).

---

### Q5 (System Design): Why centralize notifications into a dedicated service instead of having `ShipmentWorkflow` and `BillingService` send their own emails?
**Answer**:  
- **Single Preference Hub**: Users manage channel settings in one place rather than configuring preferences across 5 different services.
- **Consistent Branding & Templating**: Centralized localization and HTML email templates.
- **Cost & Rate Control**: Centralized SMS throttling and delivery auditing.
- **Service Decoupling**: Domain services emit simple business events (`ShipmentDeliveredEvent`), remaining completely oblivious to notification mechanisms, templates, or provider credentials.
