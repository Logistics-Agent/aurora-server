# Requirements Document

## Introduction

The Mail Platform is a production-grade, enterprise-secure email system built as an independent microservice
within the Aurora Server ecosystem. It provides mailbox provisioning, enterprise email security, AI-assisted
threat detection, and logistics-workflow integration. The platform is composed of two independent services:
**Stalwart Mail Server** (SMTP/IMAP/JMAP engine, run in Docker) and **Email Security Service** (a .NET 10
microservice implementing the full inbound and outbound security pipeline). The platform is designed as a
reusable, multi-tenant capability; integration with the Logistics Platform is achieved exclusively through
RabbitMQ integration events — never through shared databases or direct service calls.

---

## Glossary

- **Mail_Platform**: The combined system consisting of Stalwart Mail Server and Email Security Service.
- **Email_Security_Service**: The .NET 10 microservice that processes inbound and outbound mail security.
- **Stalwart**: The open-source mail server handling SMTP, IMAP, JMAP, TLS, DKIM, mail queue, and mailbox storage.
- **Inbound_Pipeline**: The sequential set of security checks applied to every message arriving from the internet.
- **Outbound_Pipeline**: The sequential set of validation and audit checks applied to every message sent from the platform.
- **Tenant**: An organisation provisioned on the Mail Platform, identified by a unique `TenantId`.
- **Domain**: An email domain (e.g. `company.com`) registered and owned by a Tenant.
- **Mailbox**: An email account belonging to a Staff member within a Tenant domain.
- **Alias**: An email address that redirects to one or more Mailboxes within the same Tenant.
- **System_Admin**: A privileged operator who manages domains, tenants, certificates, global policies, and quotas.
- **Tenant_Admin**: A user who manages staff accounts, mailboxes, and aliases within a single Tenant.
- **Staff**: An end-user who sends and receives mail through the Mail Platform.
- **Negotiation_Agent**: The AI service in the Logistics Platform that generates outbound emails on behalf of tenants.
- **Classifier**: The component inside Email Security Service that assigns an `EmailCategory` to each processed message.
- **Email_Category**: One of: `BookingRequest`, `ShipmentUpdate`, `Quotation`, `Complaint`, `Spam`, `Unknown`.
- **Quarantine**: A secure holding area for messages that fail security checks and require review before delivery or deletion.
- **R2**: Cloudflare R2 object storage used to persist raw EML files, attachments, metadata, audit records, and AI reports.
- **Outbox**: A transactional outbox table that guarantees at-least-once delivery of integration events to RabbitMQ.
- **Dead_Letter_Queue**: The RabbitMQ destination for messages that have exhausted all retry attempts.
- **DKIM**: DomainKeys Identified Mail — a cryptographic email authentication method.
- **SPF**: Sender Policy Framework — a DNS-based mechanism to authorise mail senders for a domain.
- **DMARC**: Domain-based Message Authentication, Reporting, and Conformance — a policy layer over SPF and DKIM.
- **ClamAV**: An open-source antivirus engine used for attachment malware scanning.
- **SpamAssassin**: An open-source spam-scoring engine.
- **Semantic_Kernel**: The Microsoft AI orchestration SDK used to invoke AI phishing-detection prompts.
- **MimeKit**: A .NET MIME parsing library used to decode and inspect email messages.
- **MailKit**: A .NET email protocol library used to communicate with Stalwart over SMTP and IMAP.
- **DnsClient**: The `DnsClient.NET` library used for DNS lookups (MX, TXT, SPF, DKIM, DMARC).
- **IdempotencyKey**: A unique identifier attached to a command or event to prevent duplicate processing.

---

## Requirements

### Requirement 1: Domain and Tenant Provisioning

**User Story:** As a System_Admin, I want to create and configure email domains and tenants, so that organisations
can send and receive mail through the Mail Platform.

#### Acceptance Criteria

1. WHEN a `TenantCreated` integration event is received from the Logistics Platform, THE Email_Security_Service
   SHALL automatically create a corresponding Domain record and provision a default administrative Mailbox for
   that Tenant without requiring manual System_Admin intervention.
2. WHEN a System_Admin creates a Domain, THE Email_Security_Service SHALL validate that the domain name is a
   well-formed FQDN and is not already registered to another Tenant before persisting it.
3. WHEN a Domain is created, THE Email_Security_Service SHALL instruct Stalwart via its HTTP management API to
   register the domain and configure DKIM key generation within 5 seconds of the Domain record being persisted.
4. IF the Stalwart management API returns an error during domain registration, THEN THE Email_Security_Service
   SHALL retry the operation up to 3 times with exponential back-off and record the failure in the audit log if
   all attempts fail.
5. THE Email_Security_Service SHALL enforce a quota limit per Domain, rejecting new Mailbox creation requests
   that would exceed the configured maximum mailbox count for that Domain.
6. WHEN a System_Admin configures a retention policy for a Domain, THE Email_Security_Service SHALL store the
   retention period in days and apply it when scheduling R2 object lifecycle expiry.
7. WHEN a System_Admin uploads a TLS certificate for a Domain, THE Email_Security_Service SHALL validate the
   certificate chain, confirm the domain name matches the Subject Alternative Name, and forward the certificate
   to Stalwart within 10 seconds.

---

### Requirement 2: Mailbox and Alias Management

**User Story:** As a Tenant_Admin, I want to create mailboxes and aliases for staff members, so that each
employee has a functioning email account.

#### Acceptance Criteria

1. WHEN a Tenant_Admin submits a create-mailbox request, THE Email_Security_Service SHALL verify that the
   requested address belongs to a Domain owned by that Tenant_Admin's Tenant before creating the Mailbox.
2. WHEN a Mailbox is created, THE Email_Security_Service SHALL instruct Stalwart to provision the mailbox
   account within 5 seconds of the Mailbox record being persisted; no password credential SHALL be set in
   Stalwart for v1, as mailbox authentication is delegated to Cognito OIDC.
3. WHEN a Tenant_Admin creates an Alias, THE Email_Security_Service SHALL validate that all target Mailbox
   addresses belong to the same Tenant before persisting the Alias.
4. WHEN a Tenant_Admin resets a Staff member's password, THE Email_Security_Service SHALL NOT set a direct
   password credential in Stalwart for v1; authentication for mailbox access is delegated entirely to Cognito
   OIDC and verified by IamTenantService, propagated through JWT claims. Direct IMAP/SMTP AUTH credential
   management (including Argon2id hashing and App Passwords) is intentionally deferred to v2 to support
   Outlook/Thunderbird clients — at that point, App Passwords (random, user-opaque) will be hashed with
   Argon2id and stored separately, not the user's own SSO password.
5. IF a Mailbox creation request would exceed the Domain quota, THEN THE Email_Security_Service SHALL return
   an error response containing the current mailbox count, the configured limit, and the Domain identifier.
6. THE Email_Security_Service SHALL resolve TenantId from authenticated current-user context and SHALL NOT
   accept a client-supplied TenantId for any Mailbox or Alias operation.

---

### Requirement 3: Inbound Mail Reception and TLS Verification

**User Story:** As a System_Admin, I want all inbound mail to be received over encrypted connections, so that
messages in transit are protected from interception.

#### Acceptance Criteria

1. WHEN an inbound SMTP connection is established, THE Stalwart SHALL require STARTTLS or SMTPS and SHALL
   reject connections that do not negotiate TLS 1.2 or higher.
2. WHEN an inbound message is accepted by Stalwart, THE Email_Security_Service SHALL retrieve the raw EML
   from Stalwart within 30 seconds of acceptance and begin Inbound_Pipeline processing.
3. WHEN the TLS handshake metadata is available for an inbound connection, THE Email_Security_Service SHALL
   record the negotiated TLS version, cipher suite, and certificate fingerprint of the sending server in the
   message audit record.
4. IF Stalwart cannot accept an inbound SMTP connection due to a queue overflow condition, THEN THE Stalwart
   SHALL return an SMTP 4xx temporary-failure response so that the sending server will retry delivery.
5. THE Email_Security_Service SHALL parse every inbound message using MimeKit and SHALL extract the
   `Message-ID`, `From`, `To`, `CC`, `Date`, `Subject`, `Content-Type`, and `Received` headers before any
   downstream security check is executed.

---

### Requirement 4: SPF, DKIM, and DMARC Validation

**User Story:** As a System_Admin, I want inbound messages to be authenticated using SPF, DKIM, and DMARC,
so that spoofed and forged emails are detected and handled.

#### Acceptance Criteria

1. WHEN an inbound message is received, THE Email_Security_Service SHALL perform an SPF DNS lookup using
   DnsClient against the envelope sender domain and SHALL record the result as `Pass`, `Fail`, `SoftFail`,
   `Neutral`, or `None`.
2. WHEN an inbound message contains a `DKIM-Signature` header, THE Email_Security_Service SHALL verify the
   signature using the public key retrieved via DnsClient DNS TXT lookup and SHALL record the result as
   `Pass` or `Fail` per signature.
3. WHEN SPF and DKIM results are available, THE Email_Security_Service SHALL evaluate the DMARC policy
   retrieved from the sender domain's `_dmarc` DNS TXT record and SHALL record the alignment result as
   `Pass`, `Fail`, or `None`.
4. IF the DMARC policy is `reject` and the DMARC evaluation result is `Fail`, THEN THE Email_Security_Service
   SHALL quarantine the message and SHALL NOT deliver it to the recipient Mailbox.
5. IF the DMARC policy is `quarantine` and the DMARC evaluation result is `Fail`, THEN THE Email_Security_Service
   SHALL move the message to Quarantine and SHALL notify the Tenant_Admin with the sender domain and message ID.
6. THE Email_Security_Service SHALL cache DNS TXT records for SPF, DKIM, and DMARC lookups in Redis with a
   TTL equal to the DNS record's own TTL value, capped at 3600 seconds, to reduce external DNS query volume.

---

### Requirement 5: Sender and Tenant Validation

**User Story:** As a System_Admin, I want inbound messages to be validated against known sender lists and
tenant boundaries, so that impersonation and cross-tenant leakage are prevented.

#### Acceptance Criteria

1. WHEN an inbound message is processed, THE Email_Security_Service SHALL verify that the envelope recipient
   address belongs to a provisioned Mailbox or Alias within a registered Tenant Domain before proceeding
   with delivery.
2. IF the envelope recipient address does not match any registered Domain, THEN THE Email_Security_Service
   SHALL reject the message with SMTP status 550 and log the rejection event with the sender IP, sender
   address, and attempted recipient.
3. WHEN an inbound message claims a `From` address belonging to a Tenant Domain, THE Email_Security_Service
   SHALL verify SPF and DKIM alignment for that domain and SHALL classify any misalignment as a spoofing
   indicator in the security score.
4. THE Email_Security_Service SHALL evaluate inbound messages against a configurable sender allow-list and
   deny-list per Tenant and SHALL block messages whose sender address or IP appears on the Tenant deny-list
   before executing further pipeline stages.
5. THE Email_Security_Service SHALL enforce Tenant isolation such that a message addressed to Tenant A is
   never accessible to Tenant B at any stage of Inbound_Pipeline processing or storage.

---

### Requirement 6: Attachment Validation and Malware Scanning

**User Story:** As a System_Admin, I want all email attachments to be scanned for malware and validated
against policy, so that ransomware and malicious files are blocked before reaching staff mailboxes.

#### Acceptance Criteria

1. WHEN an inbound or outbound message contains one or more MIME attachments, THE Email_Security_Service
   SHALL extract each attachment using MimeKit and SHALL submit each file to ClamAV over the ClamAV daemon
   socket before delivering or sending the message.
2. IF ClamAV reports a virus signature match for any attachment, THEN THE Email_Security_Service SHALL
   quarantine the entire message, record the detected signature name and attachment file name, and SHALL NOT
   deliver or send the message.
3. WHEN an inbound message attachment has a file extension matching a configurable blocked-extension list,
   THE Email_Security_Service SHALL quarantine the message and record the blocked extension and attachment
   name in the audit log.
4. WHEN an attachment exceeds the configured maximum file size per Tenant, THE Email_Security_Service SHALL
   reject the message with a descriptive error containing the attachment name, actual size in bytes, and
   the configured limit.
5. IF the ClamAV daemon is unreachable, THEN THE Email_Security_Service SHALL quarantine the message
   pending scan, record the unavailability event in metrics, and retry the scan when ClamAV becomes available
   within the configured retry window.
6. THE Email_Security_Service SHALL record the ClamAV scan result, scan duration in milliseconds, and
   ClamAV engine version for every attachment processed.

---

### Requirement 7: Spam Detection

**User Story:** As a System_Admin, I want inbound messages to be scored for spam content, so that bulk
unsolicited email is filtered before it reaches staff mailboxes.

#### Acceptance Criteria

1. WHEN an inbound message completes TLS, SPF, DKIM, DMARC, and sender validation, THE Email_Security_Service
   SHALL submit the full message in RFC 5322 format to SpamAssassin via the SpamAssassin daemon protocol and
   SHALL record the numeric spam score and the list of triggered rules.
2. WHEN a SpamAssassin score is equal to or greater than the Tenant-configured rejection threshold, THE
   Email_Security_Service SHALL quarantine the message and publish a `MessageQuarantinedAsSpam` event to
   RabbitMQ via the Outbox.
3. WHEN a SpamAssassin score is equal to or greater than the Tenant-configured tagging threshold but below
   the rejection threshold, THE Email_Security_Service SHALL prepend `[SPAM]` to the message subject,
   deliver it to the Junk folder of the recipient Mailbox, and record the tagging action in the audit log.
4. IF the SpamAssassin daemon is unreachable, THEN THE Email_Security_Service SHALL assign a default score
   of zero, record the unavailability in metrics, and continue the Inbound_Pipeline without blocking delivery.
5. THE Email_Security_Service SHALL store the spam score, triggered rules, and processing duration in
   milliseconds in the R2 spam report object for every message that is evaluated by SpamAssassin.

---

### Requirement 8: AI Phishing Detection

**User Story:** As a System_Admin, I want inbound messages to be analysed by an AI model for phishing
indicators, so that socially-engineered attacks that evade rule-based filters are detected.

#### Acceptance Criteria

1. WHEN an inbound message passes spam scoring with a score below the rejection threshold, THE
   Email_Security_Service SHALL invoke Semantic_Kernel with a structured phishing-detection prompt
   containing the message subject, body text, sender address, and extracted URLs.
2. WHEN Semantic_Kernel returns a phishing probability score, THE Email_Security_Service SHALL record
   the score as a decimal between 0.0 and 1.0, the reasoning summary, and the AI model identifier in the
   R2 AI report object.
3. WHEN the AI phishing probability score is greater than or equal to the Tenant-configured phishing
   quarantine threshold, THE Email_Security_Service SHALL quarantine the message and publish a
   `MessageQuarantinedAsPhishing` event to RabbitMQ via the Outbox.
4. IF the Semantic_Kernel invocation fails or times out after 10 seconds, THEN THE Email_Security_Service
   SHALL log the failure, assign a phishing score of 0.0, and continue the Inbound_Pipeline without
   blocking message delivery.
5. THE Email_Security_Service SHALL not send full message bodies to external AI providers unless the
   Tenant has explicitly enabled cloud AI processing; WHERE cloud AI is disabled, THE Email_Security_Service
   SHALL use an Azure OpenAI or locally-hosted model endpoint instead.

---

### Requirement 9: Email Classification and Logistics Event Publication

**User Story:** As a Logistics Platform operator, I want inbound emails to be classified by business
intent, so that the correct downstream workflow is triggered automatically.

#### Acceptance Criteria

1. WHEN an inbound message completes all Inbound_Pipeline security checks without being quarantined,
   THE Classifier SHALL assign exactly one Email_Category to the message based on subject, body, and
   sender heuristics.
2. THE Email_Security_Service SHALL publish a typed RabbitMQ integration event corresponding to the
   assigned Email_Category: `BookingRequestReceived`, `ShipmentUpdateReceived`, `QuotationReceived`,
   `ComplaintReceived`, `SpamDetected`, or `UnknownEmailReceived`.
3. WHEN a classification event is published, THE Email_Security_Service SHALL include in the event
   payload: `MessageId`, `TenantId`, `SenderAddress`, `RecipientAddress`, `Subject`, `EmailCategory`,
   `R2RawEmlPath`, `R2AttachmentPaths`, `ProcessedAt` (UTC), and `PipelineResultSummary`.
4. THE Email_Security_Service SHALL publish all classification events through the transactional Outbox
   pattern, guaranteeing at-least-once delivery to RabbitMQ.
5. WHEN the Classifier cannot determine a specific category with confidence above the configured minimum
   threshold, THE Email_Security_Service SHALL assign the category `Unknown` and publish the
   `UnknownEmailReceived` event.
6. THE Email_Security_Service SHALL be deployable and operable independently of whether any Logistics
   Platform consumer is active; absent consumers SHALL NOT cause event publication to fail.
7. FOR ALL valid inbound messages, the Email_Category assigned by the Classifier SHALL be reproducible
   from the stored R2 raw EML and metadata without re-invoking external AI, enabling audit replay.

---

### Requirement 10: Object Storage — R2 Layout and Lifecycle

**User Story:** As a System_Admin, I want all mail artifacts stored in Cloudflare R2 with a predictable
naming convention and lifecycle policy, so that storage is auditable, cost-effective, and recoverable.

#### Acceptance Criteria

1. WHEN an inbound message is processed, THE Email_Security_Service SHALL upload the raw EML to R2 using
   the object key pattern `tenants/{tenantId}/inbound/{year}/{month}/{day}/{messageId}/raw.eml` within
   60 seconds of pipeline completion.
2. WHEN an inbound message contains attachments, THE Email_Security_Service SHALL upload each attachment
   to R2 using the key pattern
   `tenants/{tenantId}/inbound/{year}/{month}/{day}/{messageId}/attachments/{attachmentIndex}_{filename}`.
3. WHEN pipeline processing is complete, THE Email_Security_Service SHALL upload a JSON metadata document
   to R2 at `tenants/{tenantId}/inbound/{year}/{month}/{day}/{messageId}/metadata.json` containing all
   extracted headers, security check results, classification, and R2 object keys.
4. WHEN an AI phishing analysis is performed, THE Email_Security_Service SHALL upload the AI report to
   R2 at `tenants/{tenantId}/inbound/{year}/{month}/{day}/{messageId}/ai_report.json`.
5. WHEN a message is quarantined as spam, THE Email_Security_Service SHALL upload the spam report to R2
   at `tenants/{tenantId}/inbound/{year}/{month}/{day}/{messageId}/spam_report.json`.
6. THE Email_Security_Service SHALL apply per-Domain retention policies by setting R2 object metadata
   `x-amz-expiry-days` equal to the Domain's configured retention period on all objects uploaded for that
   Domain's messages.
7. THE Email_Security_Service SHALL use HTTPS-only access to the R2 private bucket and SHALL NOT generate
   public URLs for any stored object; all retrieval SHALL use pre-signed URLs with a maximum expiry of
   3600 seconds.

---

### Requirement 11: Metadata Persistence in PostgreSQL

**User Story:** As a developer, I want email metadata to be stored in PostgreSQL so that message
history, audit trails, and tenant-scoped queries can be executed efficiently without retrieving
full message bodies from R2.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL persist a `ProcessedMessage` record in PostgreSQL for every
   inbound and outbound message that enters the pipeline, containing: `Id`, `TenantId`, `MessageId`,
   `Direction` (`Inbound`/`Outbound`), `SenderAddress`, `RecipientAddresses`, `Subject`, `ReceivedAt`,
   `ProcessedAt`, `EmailCategory`, `PipelineStatus`, `SpamScore`, `PhishingScore`, `IsQuarantined`,
   `R2RawEmlPath`, and `AuditId`.
2. THE Email_Security_Service SHALL persist a `SecurityCheckResult` record per pipeline stage per
   message, referencing the parent `ProcessedMessage` by ID, containing the stage name, result, detail
   JSON, and duration in milliseconds.
3. THE Email_Security_Service SHALL apply tenant-scoped filtering to all PostgreSQL queries involving
   `ProcessedMessage` and `SecurityCheckResult` records, rejecting queries that lack a verified TenantId.
4. WHEN a Tenant_Admin queries message history, THE Email_Security_Service SHALL return results ordered
   by `ReceivedAt` descending and SHALL support pagination using a cursor-based approach with a maximum
   page size of 100 records.
5. THE Email_Security_Service SHALL store all PostgreSQL timestamps in UTC and SHALL not rely on database
   server timezone settings.

---

### Requirement 12: Outbound Mail Pipeline

**User Story:** As a Negotiation_Agent, I want outbound emails to be validated and audited before
dispatch, so that malicious content, policy violations, and rate-limit abuse are prevented.

#### Acceptance Criteria

1. WHEN the Negotiation_Agent submits an outbound send request, THE Email_Security_Service SHALL execute
   the Outbound_Pipeline in this order: attachment validation → policy validation → AI risk scoring →
   rate limit check → audit record creation → Stalwart SMTP submission.
2. WHEN the Outbound_Pipeline attachment validation stage runs, THE Email_Security_Service SHALL apply
   the same ClamAV scan, blocked-extension check, and size-limit check defined in Requirement 6.
3. WHEN the policy validation stage runs, THE Email_Security_Service SHALL verify that the sender address
   belongs to the authenticated Tenant, the recipient domain is not on the Tenant deny-list, and the
   message does not contain content matching any configured keyword policy.
4. WHEN the AI risk scoring stage runs, THE Email_Security_Service SHALL invoke Semantic_Kernel to
   evaluate whether the outbound message content represents a Business Email Compromise risk and SHALL
   record the risk score, reasoning, and model identifier.
5. WHEN the rate limit stage runs, THE Email_Security_Service SHALL enforce a per-Mailbox outbound
   rate limit using a Redis sliding-window counter with a configurable window duration and message count
   limit; IF the limit is exceeded, THEN THE Email_Security_Service SHALL reject the send request with
   an error containing the current count, the limit, and the window reset time.
6. WHEN Stalwart SMTP submission succeeds, THE Email_Security_Service SHALL record the SMTP response
   code, Stalwart queue ID, and submission timestamp in the `ProcessedMessage` audit record.
7. IF Stalwart SMTP submission fails with a 4xx transient error, THEN THE Email_Security_Service SHALL
   retry submission up to 3 times with exponential back-off before moving the message to the
   Dead_Letter_Queue.

---

### Requirement 13: Quarantine Management

**User Story:** As a Tenant_Admin, I want to review, release, and delete quarantined messages, so that
legitimate messages blocked by security checks can be recovered and false positives can be managed.

#### Acceptance Criteria

1. WHEN a message is quarantined, THE Email_Security_Service SHALL store the quarantine record with:
   `Id`, `TenantId`, `MessageId`, `QuarantineReason`, `QuarantinedAt`, `Status` (`Pending`/`Released`/
   `Deleted`), `ReviewedBy`, and `ReviewedAt`.
2. WHEN a Tenant_Admin releases a quarantined message, THE Email_Security_Service SHALL instruct
   Stalwart to deliver the message to the original recipient Mailbox, update the quarantine record
   `Status` to `Released`, and record the reviewer identity and release timestamp.
3. WHEN a Tenant_Admin deletes a quarantined message, THE Email_Security_Service SHALL mark the
   quarantine record `Status` as `Deleted`, delete the quarantine copy from Stalwart, and retain the
   R2 stored objects for the duration of the Domain retention policy.
4. THE Email_Security_Service SHALL enforce that only a Tenant_Admin belonging to the same Tenant as
   the recipient Mailbox may release or delete a quarantined message.
5. WHILE a message is in `Pending` quarantine status beyond the Tenant-configured auto-delete window,
   THE Email_Security_Service SHALL automatically transition the message to `Deleted` status, remove
   it from Stalwart quarantine storage, and record the auto-deletion in the audit log.

---

### Requirement 14: Rate Limiting and Replay Attack Prevention

**User Story:** As a System_Admin, I want the platform to enforce rate limits and detect replay attacks,
so that abuse, flooding, and credential theft are mitigated.

#### Acceptance Criteria

1. WHEN an SMTP connection is accepted by Stalwart, THE Stalwart SHALL enforce a per-source-IP
   connection rate limit, rejecting connections that exceed the configured maximum connections per minute
   with an SMTP 421 response.
2. THE Email_Security_Service SHALL maintain a per-Mailbox inbound message counter in Redis using a
   sliding window of 60 seconds and SHALL quarantine messages from a single sender that exceed the
   configured inbound rate limit within that window.
3. WHEN an inbound message `Message-ID` matches a `Message-ID` already recorded in the PostgreSQL
   `ProcessedMessage` table for the same TenantId within the past 24 hours, THE Email_Security_Service
   SHALL classify the duplicate as a replay attempt, quarantine it, and record the original `ProcessedAt`
   timestamp in the audit entry.
4. THE Email_Security_Service SHALL use Redis `SETNX` with a TTL of 86400 seconds to record processed
   `Message-ID` values per Tenant, providing O(1) idempotency check performance for replay detection.
5. WHEN an outbound submission from a single Mailbox exceeds the configured maximum messages per hour
   limit in Redis, THE Email_Security_Service SHALL reject subsequent send requests for that Mailbox for
   the remainder of the current hour window and return an error containing the reset time.

---

### Requirement 15: Audit Logging

**User Story:** As a System_Admin, I want a complete, immutable audit trail for every message and
administrative action, so that security incidents can be investigated and compliance obligations are met.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL create an immutable audit record for every inbound message,
   outbound message, quarantine action, release action, mailbox creation, password reset, domain
   provisioning, and policy change.
2. WHEN an audit record is created, THE Email_Security_Service SHALL persist it to both PostgreSQL
   and R2 (at `tenants/{tenantId}/audit/{year}/{month}/{auditId}.json`) within the same database
   transaction, using the Outbox to guarantee R2 upload when the transaction commits.
3. THE Email_Security_Service SHALL include in every audit record: `AuditId`, `TenantId`, `ActorId`,
   `ActorType` (`System`/`TenantAdmin`/`Staff`/`Service`), `Action`, `ResourceType`, `ResourceId`,
   `Timestamp` (UTC), `ClientIp`, `Result` (`Success`/`Failure`), and `DetailJson`.
4. THE Email_Security_Service SHALL not allow any actor, including System_Admin, to modify or delete
   an audit record through any API endpoint.
5. WHEN a Tenant_Admin queries audit records, THE Email_Security_Service SHALL return only audit
   records belonging to that Tenant_Admin's Tenant, filtered by the verified TenantId from
   authentication context.

---

### Requirement 16: Idempotency, Retry, and Dead Letter Queue

**User Story:** As a System_Admin, I want all pipeline stages to be idempotent and retryable, so
that transient failures do not cause message loss or duplicate processing.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL assign a unique `PipelineExecutionId` to each message when it
   enters the Inbound_Pipeline or Outbound_Pipeline and SHALL use this ID as the IdempotencyKey for
   all downstream stage operations.
2. WHEN a pipeline stage fails with a transient error, THE Email_Security_Service SHALL retry the
   stage up to the configured maximum retry count using exponential back-off with jitter, with an
   initial delay of 1 second and a maximum delay of 30 seconds.
3. WHEN a message has exhausted all pipeline retry attempts, THE Email_Security_Service SHALL move
   the message context to the Dead_Letter_Queue in RabbitMQ, record the failure reason, last error
   message, retry count, and final failure timestamp in the `ProcessedMessage` record.
4. WHEN a RabbitMQ consumer receives an event that has already been processed (identified by
   matching `MessageId` and `TenantId` in the `ProcessedMessage` table), THE Email_Security_Service
   SHALL acknowledge the message without re-processing and SHALL record a deduplication log entry.
5. THE Email_Security_Service SHALL expose a Dead_Letter_Queue requeue API that allows System_Admin
   to resubmit a dead-lettered message for pipeline re-execution, resetting the retry counter.

---

### Requirement 17: Observability — Metrics, Tracing, and Logging

**User Story:** As a System_Admin, I want the Mail Platform to emit structured logs, distributed
traces, and Prometheus metrics, so that operational health and security incidents are visible in
Grafana dashboards.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL emit structured JSON logs using Serilog with minimum fields:
   `Timestamp`, `Level`, `Service`, `TenantId`, `MessageId`, `TraceId`, `SpanId`, `Message`,
   and `Exception` (when present).
2. THE Email_Security_Service SHALL ship all logs to Loki using the Serilog Loki sink and SHALL tag
   each log entry with `service=email-security`, `tenant={tenantId}`, and `env={environment}`.
3. THE Email_Security_Service SHALL instrument every pipeline stage with an OpenTelemetry span,
   recording the stage name, duration, result, and error (if any) as span attributes.
4. THE Email_Security_Service SHALL expose a `/metrics` Prometheus endpoint publishing the following
   counters and histograms: `mail_inbound_total`, `mail_outbound_total`, `mail_quarantined_total`,
   `mail_pipeline_stage_duration_seconds` (by stage), `mail_spam_score_histogram`,
   `mail_phishing_score_histogram`, `mail_clamav_scan_duration_seconds`, and `mail_dlq_total`.
5. THE Email_Security_Service SHALL expose a `/health` endpoint returning HTTP 200 with a JSON body
   when all critical dependencies (PostgreSQL, Redis, RabbitMQ, Stalwart, ClamAV, SpamAssassin) are
   reachable, and HTTP 503 with a JSON body listing degraded or failed dependencies when any are not.
6. THE Stalwart SHALL expose its own metrics endpoint, and THE Email_Security_Service SHALL scrape
   and re-expose Stalwart queue depth, delivery success rate, and bounce rate as Prometheus metrics
   under the `mail_stalwart_` prefix.
7. THE Email_Security_Service SHALL propagate `x-correlation-id` and `x-request-id` from incoming gRPC
   metadata or HTTP headers into all Serilog log entries and OpenTelemetry spans as structured attributes
   `correlation_id` and `request_id`, enabling end-to-end request tracing across the BFF →
   Email_Security_Service boundary.

---

### Requirement 18: DKIM Signing for Outbound Mail

**User Story:** As a System_Admin, I want all outbound messages to be DKIM-signed with per-domain
keys, so that receiving mail servers can verify message authenticity.

#### Acceptance Criteria

1. WHEN an outbound message is submitted to Stalwart for delivery, THE Stalwart SHALL sign the message
   using the DKIM private key registered for the sender's Domain using RSA-SHA256 with a minimum key
   length of 2048 bits.
2. WHEN a Domain is provisioned, THE Email_Security_Service SHALL instruct Stalwart to generate a
   DKIM key pair, store the private key in Stalwart's key store, and return the DNS TXT record value
   for the public key to the System_Admin.
3. WHEN a System_Admin rotates a Domain's DKIM key, THE Email_Security_Service SHALL generate a new
   key pair with a new DKIM selector, publish the new public key DNS record value, and retain the
   old selector active for a configurable overlap period to allow in-flight messages to validate.
4. IF DKIM signing fails for an outbound message due to a missing or expired key, THEN THE
   Email_Security_Service SHALL quarantine the outbound message, alert the System_Admin, and record
   the failure in the audit log.

---

### Requirement 19: Header Analysis and Forgery Detection

**User Story:** As a System_Admin, I want inbound message headers to be analysed for forgery and
anomaly indicators, so that header injection and Business Email Compromise attacks are detected.

#### Acceptance Criteria

1. WHEN an inbound message is processed, THE Email_Security_Service SHALL parse the full `Received`
   header chain using MimeKit and SHALL extract the hop sequence, source IPs, relay hostnames, and
   per-hop timestamps.
2. THE Email_Security_Service SHALL detect and record the following header anomaly indicators:
   `From`/`Reply-To` domain mismatch, `From`/`Envelope-From` domain mismatch, `Received` chain IP
   inconsistency with SPF authorised senders, future-dated `Date` header beyond a 5-minute tolerance,
   and presence of duplicate `Message-ID` headers.
3. WHEN one or more header anomaly indicators are detected, THE Email_Security_Service SHALL add each
   indicator to the message security score as a weighted penalty and SHALL record the indicators in the
   `SecurityCheckResult` record for the header-analysis stage.
4. IF the aggregate security score from header anomaly penalties exceeds the Tenant-configured
   header-forgery quarantine threshold, THEN THE Email_Security_Service SHALL quarantine the message.
5. THE Email_Security_Service SHALL store the parsed header chain as structured JSON in the R2
   metadata object for every processed inbound message to support forensic review.

---

### Requirement 20: Multi-Tenancy and Tenant Isolation

**User Story:** As a System_Admin, I want strict tenant isolation across all Mail Platform data and
operations, so that one tenant's mail data is never accessible to another tenant.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL include `TenantId` as a non-nullable discriminator column on
   all PostgreSQL tables that store tenant-owned data (`ProcessedMessage`, `SecurityCheckResult`,
   `QuarantineRecord`, `Mailbox`, `Domain`, `Alias`, `AuditRecord`).
2. THE Email_Security_Service SHALL apply a global query filter on all EF Core DbSet queries for
   tenant-owned entities, ensuring `TenantId` equality is always enforced at the database level.
3. THE Email_Security_Service SHALL resolve `TenantId` exclusively from the authenticated JWT bearer
   claim or trusted event metadata, and SHALL return HTTP 403 or gRPC `PERMISSION_DENIED` for any
   request where tenant context cannot be verified.
4. WHEN an inbound message is stored in R2, THE Email_Security_Service SHALL use the tenant-scoped
   object key prefix `tenants/{tenantId}/` for all objects, ensuring R2 access policies can enforce
   path-based isolation.
5. THE Email_Security_Service SHALL not log or expose TenantId values in publicly-visible error
   responses, stack traces, or HTTP headers.
6. WHEN internal service-to-service calls are made between the Email_Security_Service and other Aurora
   Server services (e.g., BFF, IamTenantService), THE Email_Security_Service SHALL authenticate using JWT
   bearer tokens; header fields `x-correlation-id` and `x-request-id` MAY be forwarded for tracing
   purposes, but identity and tenant context SHALL be resolved exclusively from the JWT claims — the header
   `x-role-id` SHALL NOT be read or trusted.

---

### Requirement 21: MIME Parsing (Parser and Round-Trip)

**User Story:** As a developer, I want the Email_Security_Service to correctly parse and reconstruct
RFC 5322 and MIME messages, so that all pipeline stages receive accurate message data and parsed
messages can be audited by re-parsing stored artefacts.

#### Acceptance Criteria

1. WHEN a raw EML byte stream is received, THE MimeParser SHALL parse it into a structured
   `ParsedMessage` object containing headers, body parts, attachments, and encoding metadata using
   MimeKit without throwing an exception for any well-formed RFC 5322 message.
2. IF a raw EML byte stream is malformed (missing required headers, invalid MIME boundary, or
   truncated body), THEN THE MimeParser SHALL return a `ParseResult` with `IsValid = false` and a
   descriptive error list rather than throwing an exception.
3. THE MimePrinter SHALL serialise a `ParsedMessage` object back into a valid RFC 5322 EML byte
   stream preserving all original headers, body content, and attachment data.
4. FOR ALL well-formed `ParsedMessage` objects, parsing then printing then parsing SHALL produce
   a `ParsedMessage` structurally equivalent to the original (round-trip property): header values,
   body text, attachment byte content, and MIME part counts SHALL be identical across both parses.
5. THE MimeParser SHALL correctly decode all standard content-transfer-encodings: `7bit`, `8bit`,
   `base64`, `quoted-printable`, and `binary`.
6. THE MimeParser SHALL extract all `Content-Disposition: attachment` and `Content-Disposition: inline`
   MIME parts as discrete attachment objects, preserving the original filename, content-type, and
   byte content.

---

### Requirement 22: gRPC API Surface

**User Story:** As a developer integrating with the Mail Platform, I want well-defined gRPC APIs for
all management and pipeline operations, so that the Email Security Service can be called reliably
from other services and the admin portal.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL expose gRPC service definitions in a `.proto` file for the
   following operations: `ProvisionDomain`, `CreateMailbox`, `CreateAlias`, `ResetPassword`,
   `SubmitOutboundMessage`, `GetProcessedMessage`, `ListProcessedMessages`, `GetQuarantineRecord`,
   `ListQuarantineRecords`, `ReleaseQuarantine`, `DeleteQuarantine`, `GetAuditRecords`, and
   `RequeueDeadLetter`.
2. WHEN a gRPC call is received, THE Email_Security_Service SHALL validate all required fields
   against the proto contract and SHALL return gRPC status `INVALID_ARGUMENT` with a field-level
   violation detail for any missing or malformed required field.
3. THE Email_Security_Service SHALL not expose internal stack traces, database error details, or
   tenant data in gRPC error responses; error messages SHALL be human-readable descriptions only.
4. THE Email_Security_Service SHALL enforce authentication on all gRPC endpoints using bearer token
   metadata validation and SHALL return gRPC status `UNAUTHENTICATED` for requests without a valid token.
5. WHERE a gRPC list operation returns more records than the maximum page size, THE
   Email_Security_Service SHALL use cursor-based pagination with a `NextPageToken` field in the
   response message.

---

### Requirement 23: Deployment and Infrastructure

**User Story:** As a developer, I want the Mail Platform to be fully containerised and deployable on
Docker Compose locally and on AKS in production, so that the environment is reproducible and
independently deployable.

#### Acceptance Criteria

1. THE Mail_Platform SHALL provide a `docker-compose.dev.yml` configuration that starts Stalwart,
   Email_Security_Service, PostgreSQL, Redis, RabbitMQ, ClamAV, SpamAssassin, Grafana, Prometheus,
   and Loki as named containers with health check definitions for each service.
2. THE Email_Security_Service SHALL be packaged as a Docker image using a multi-stage build that
   produces a final image based on `mcr.microsoft.com/dotnet/aspnet:10.0` with no build tools in
   the runtime layer.
3. WHEN deployed to AKS, THE Mail_Platform SHALL use cert-manager to issue and renew TLS certificates
   for the Stalwart SMTP, IMAPS, and JMAP endpoints and for the Email_Security_Service gRPC endpoint.
4. WHEN deployed to AKS, THE Mail_Platform SHALL use Ingress NGINX as the ingress controller for
   HTTPS traffic to the webmail client and admin portal and SHALL configure separate IngressClass
   resources for mail-specific TCP routing.
5. THE Email_Security_Service SHALL read all secrets (PostgreSQL connection string, Redis password,
   RabbitMQ credentials, R2 access key, Stalwart admin token, ClamAV socket path) from environment
   variables or Kubernetes Secrets and SHALL NOT contain any secret values in source code or Docker
   images.
6. THE Mail_Platform SHALL expose Kubernetes liveness and readiness probes on the
   Email_Security_Service `/health/live` and `/health/ready` endpoints, enabling AKS to restart
   unhealthy pods automatically.

---

### Requirement 24: SMTP Protocol Compliance

**User Story:** As a mail server operator, I want Stalwart to be fully RFC-compliant for SMTP, IMAP,
and JMAP, so that interoperability with third-party mail clients and servers is guaranteed.

#### Acceptance Criteria

1. THE Stalwart SHALL implement SMTP as defined in RFC 5321, accepting `EHLO`, `MAIL FROM`, `RCPT TO`,
   `DATA`, `QUIT`, `RSET`, `NOOP`, and `VRFY` commands.
2. THE Stalwart SHALL implement IMAP4rev2 as defined in RFC 9051, supporting `LOGIN`, `SELECT`,
   `FETCH`, `STORE`, `SEARCH`, `APPEND`, `COPY`, `MOVE`, `EXPUNGE`, and `LOGOUT` commands.
3. WHEN a mail client connects over IMAPS (port 993), THE Stalwart SHALL require TLS 1.2 or higher
   before accepting any IMAP commands.
4. THE Stalwart SHALL support JMAP for Mail as defined in RFC 8621, exposing `Mailbox`, `Email`,
   `EmailSubmission`, and `Thread` method calls over HTTPS.
5. THE Stalwart SHALL enforce SMTP message size limits per Domain, returning SMTP status 552 when
   a submitted message exceeds the configured maximum message size.

---

### Requirement 25: Security Threat Coverage

**User Story:** As a System_Admin, I want the Mail Platform's security pipeline to explicitly address
the defined threat model, so that each identified threat class is mitigated by at least one
enforceable control.

#### Acceptance Criteria

1. THE Inbound_Pipeline SHALL mitigate **Spoofing** through SPF validation (Requirement 4), DMARC
   enforcement (Requirement 4), and sender domain validation (Requirement 5).
2. THE Inbound_Pipeline SHALL mitigate **Phishing** through AI phishing detection (Requirement 8)
   and header forgery detection (Requirement 19).
3. THE Inbound_Pipeline SHALL mitigate **Spam** through SpamAssassin scoring (Requirement 7) and
   inbound rate limiting (Requirement 14).
4. THE Inbound_Pipeline SHALL mitigate **Malware and Ransomware Attachments** through ClamAV scanning
   and extension blocking (Requirement 6).
5. THE Inbound_Pipeline SHALL mitigate **Replay Attacks** through Message-ID deduplication with a
   24-hour Redis TTL (Requirement 14).
6. THE Outbound_Pipeline SHALL mitigate **Business Email Compromise** through AI risk scoring and
   policy validation (Requirement 12).
7. THE Inbound_Pipeline SHALL mitigate **Header Forgery** through header chain analysis and anomaly
   scoring (Requirement 19).
8. THE Mail_Platform SHALL mitigate **Credential Theft** by enforcing TLS 1.2 or higher on all SMTP and IMAP
   connections (Requirements 3 and 24) and by delegating mailbox authentication to Cognito OIDC in v1 —
   eliminating locally-stored passwords from the attack surface entirely (Requirement 2); IMAP/SMTP AUTH
   with Argon2id App Passwords is deferred to v2.

---

### Requirement 26: Logistics Platform Integration

**User Story:** As a Logistics Platform operator, I want the Mail Platform to integrate with the
Aurora Server through RabbitMQ integration events only, so that coupling is minimal and each
platform evolves independently.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL consume the `TenantCreated` integration event from the
   Logistics Platform RabbitMQ exchange using MassTransit and SHALL auto-provision a Domain and
   administrative Mailbox for the new Tenant.
2. THE Email_Security_Service SHALL publish classification events (`BookingRequestReceived`,
   `ShipmentUpdateReceived`, `QuotationReceived`, `ComplaintReceived`, `SpamDetected`,
   `UnknownEmailReceived`) to a dedicated RabbitMQ exchange that Logistics Platform services
   can subscribe to.
3. THE Email_Security_Service SHALL not access the Logistics Platform's PostgreSQL database
   directly; all cross-platform data exchange SHALL occur exclusively through RabbitMQ events.
4. THE Email_Security_Service SHALL not share a RabbitMQ virtual host with other Logistics
   Platform services; a dedicated virtual host SHALL be used for Mail Platform exchanges and queues.
5. WHEN the Logistics Platform's RabbitMQ is unreachable, THE Email_Security_Service SHALL continue
   processing inbound and outbound mail, buffer undelivered events in the Outbox, and resume
   publishing when connectivity is restored.
6. THE Email_Security_Service SHALL include `TenantId` in every published integration event and
   SHALL validate that the `TenantId` in consumed events matches a provisioned Tenant before
   executing any provisioning action.

---

### Requirement 27: Clean Architecture and Folder Structure

**User Story:** As a developer, I want the Email_Security_Service source code to follow a single-project
Clean Architecture layout matching the IamTenantService convention, so that the codebase is consistent,
simple to navigate, and avoids unnecessary cross-project reference complexity.

#### Acceptance Criteria

1. THE Email_Security_Service SHALL be structured as a single .NET project (`MailService.csproj`) containing
   the following top-level folders: `Domain/`, `Application/`, `Infrastructure/`, and `GrpcServices/`; the
   project SHALL compile as a single assembly.
2. THE `GrpcServices/` folder SHALL contain the gRPC service implementations corresponding to the `.proto`
   contract operations (`ProvisionDomain`, `CreateMailbox`, `CreateAlias`, `ResetPassword`,
   `SubmitOutboundMessage`, `GetProcessedMessage`, `ListProcessedMessages`, `GetQuarantineRecord`,
   `ListQuarantineRecords`, `ReleaseQuarantine`, `DeleteQuarantine`, `GetAuditRecords`, `RequeueDeadLetter`);
   `Program.cs` SHALL be placed at the project root alongside `appsettings.json` and `Dockerfile`, not
   inside `GrpcServices/`.
3. THE `Domain/` folder SHALL contain only domain entities, value objects, enums, domain events, and domain
   service interfaces; code inside `Domain/` SHALL NOT reference types from `Infrastructure/`, `Application/`,
   or `GrpcServices/` — this dependency rule SHALL be enforced by code review and optionally by a Roslyn
   analyzer, not by compile-time project references.
4. THE `Application/` folder SHALL contain commands, queries, handlers, DTOs, and pipeline behaviours; code
   inside `Application/` SHALL reference only types from `Domain/` and SHALL NOT reference types from
   `Infrastructure/` or `GrpcServices/` — enforced by code review and optionally by a Roslyn analyzer.
5. THE `Infrastructure/` folder SHALL contain all external integration implementations (EF Core DbContext,
   Redis, RabbitMQ/MassTransit, MailKit, ClamAV client, DnsClient, Stalwart HTTP client, Cloudflare R2
   client, Serilog, OpenTelemetry); infrastructure code MAY reference `Domain/` and `Application/` types
   but SHALL NOT reference `GrpcServices/`.
6. THE Email_Security_Service source code SHALL reside at `src/dotnet/MailService/` consistent with the
   Aurora Server project layout.

---

### Requirement 28: Automatic Mailbox Provisioning from IamTenantService Events

**User Story:** As a System_Admin, I want mailboxes to be automatically provisioned when user accounts are
created in IamTenantService, so that staff members have a functioning email account without requiring manual
intervention.

#### Acceptance Criteria

1. WHEN a user-provisioning integration event (e.g., `UserCreated` or `StaffCreated`) is received from
   IamTenantService via RabbitMQ, THE Email_Security_Service SHALL automatically provision a Mailbox for the
   user under the Tenant's registered Domain if a Mailbox for that address does not already exist.
2. WHEN provisioning a Mailbox from a user-provisioning event, THE Email_Security_Service SHALL derive the
   mailbox address as `{username}@{tenantDomain}` using the username and TenantId from the event payload,
   and SHALL resolve the Tenant's Domain from the local Domain registry.
3. THE Email_Security_Service SHALL NOT set a password credential in Stalwart during automatic mailbox
   provisioning; authentication for the provisioned Mailbox is delegated entirely to Cognito OIDC, verified
   by IamTenantService, and propagated via JWT claims or gRPC metadata following the Shared.Security pattern.
4. IF a user-provisioning event is received for a TenantId that has no registered Domain in the Mail Platform,
   THEN THE Email_Security_Service SHALL defer provisioning, record a `MailboxProvisioningDeferred` event to
   the Outbox with the original event payload, and retry provisioning when a `DomainProvisioned` event for
   that TenantId is subsequently received.
5. WHEN automatic mailbox provisioning succeeds, THE Email_Security_Service SHALL publish a
   `MailboxProvisioned` integration event to RabbitMQ via the Outbox, containing `MailboxId`, `TenantId`,
   `MailboxAddress`, `ProvisionedAt` (UTC), and `SourceEventId`.
6. THE Email_Security_Service SHALL apply idempotency using the source event `MessageId` as the
   IdempotencyKey, ensuring that duplicate user-provisioning events do not result in duplicate Mailbox
   creation attempts.
