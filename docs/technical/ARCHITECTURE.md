# Aurora Server — System Architecture & Topology Specification

> **Source-of-Truth Architectural Audit**: Detailed technical blueprints, sequence models, component relationships, database boundaries, and security perimeters.

---

## 1. System Topology & Network Perimeters

```mermaid
graph TD
    subgraph ClientLayer ["Client Layer"]
        SPA["Aurora Web App (React / Next.js)"]
        Mobile["Aurora Mobile App"]
    end

    subgraph IngressGateway ["Ingress & Reverse Proxy Layer"]
        YARP["YARP API Gateway (:5000 / :443)"]
    end

    subgraph BFFLayer ["Backend-For-Frontend (BFF) Layer"]
        StaffBFF["Staff.Bff (:5001)<br/>(Operators / Customs / Finance)"]
        AdminBFF["Admin.Bff (:5002)<br/>(Tenant Admins)"]
        SystemBFF["System.Bff (:5003)<br/>(Super Administrators)"]
        RealtimeHub["RealtimeHub (:5004)<br/>(WebSocket Gateway)"]
    end

    subgraph SecurityInfra ["Security & Identity"]
        Cognito["AWS Cognito / Azure AD"]
        RedisCluster["Redis Cluster (Distributed Cache / Rate Limiter)"]
    end

    subgraph CoreServicesDotNet [".NET 10 Microservices (gRPC)"]
        IamSvc["IamTenant (:5100)"]
        ShipmentSvc["ShipmentWorkflow (:5101)"]
        RouteSvc["RoutePlanningAgent (:5102)"]
        MailSvc["MailService (:5103)"]
        OcrSvc["DocumentOcr (:5104)"]
        ComplianceSvc["RegulatoryCompliance (:5105)"]
        GpsSvc["GpsTracking (:5106)"]
        NotificationSvc["Notification (:5107)"]
    end

    subgraph JavaServices ["Java 21 / Spring Boot Microservices"]
        AiGovSvc["ai-governance (:5200)"]
        DevOpsSvc["devops-agent (:5201)"]
    end

    subgraph NestJsServices ["NestJS Microservices"]
        BillingSvc["billing-service (:5300)"]
        FinancialSvc["financial-service (:5301)"]
        NegotiationSvc["negotiation-agent (:5302)"]
        AssistantSvc["customer-assistant (:5303)"]
    end

    subgraph MessageBus ["Event Streaming & Transactional Outbox"]
        RabbitMQ["RabbitMQ Cluster (AMQP)"]
    end

    SPA -->|HTTPS| YARP
    Mobile -->|HTTPS| YARP
    SPA -->|WSS| RealtimeHub

    YARP -->|Route /api/v1/*| StaffBFF
    YARP -->|Route /api/v1/admin/*| AdminBFF
    YARP -->|Route /api/v1/system/*| SystemBFF

    StaffBFF --> Cognito
    AdminBFF --> Cognito
    SystemBFF --> Cognito

    StaffBFF --> RedisCluster
    AdminBFF --> RedisCluster
    SystemBFF --> RedisCluster
    RealtimeHub --> RedisCluster

    StaffBFF -->|gRPC| IamSvc
    StaffBFF -->|gRPC| ShipmentSvc
    StaffBFF -->|gRPC| RouteSvc
    StaffBFF -->|gRPC| MailSvc
    StaffBFF -->|gRPC| OcrSvc
    StaffBFF -->|gRPC| ComplianceSvc
    StaffBFF -->|gRPC| GpsSvc
    StaffBFF -->|gRPC| NotificationSvc
    StaffBFF -->|gRPC| BillingSvc
    StaffBFF -->|gRPC| FinancialSvc
    StaffBFF -->|gRPC| NegotiationSvc
    StaffBFF -->|HTTP| AssistantSvc

    AdminBFF -->|gRPC| IamSvc
    AdminBFF -->|gRPC| MailSvc
    AdminBFF -->|gRPC| RouteSvc
    AdminBFF -->|gRPC| ComplianceSvc
    AdminBFF -->|gRPC| AiGovSvc

    SystemBFF -->|gRPC| IamSvc
    SystemBFF -->|gRPC| MailSvc
    SystemBFF -->|gRPC| ComplianceSvc
    SystemBFF -->|gRPC| DevOpsSvc

    %% Internal Microservice to Microservice gRPC
    RouteSvc -->|gRPC| ComplianceSvc
    BillingSvc -->|gRPC| FinancialSvc
    NegotiationSvc -->|gRPC| FinancialSvc
    AssistantSvc -->|gRPC| ComplianceSvc
    AssistantSvc -->|gRPC| ShipmentSvc
    AssistantSvc -->|gRPC| BillingSvc

    %% AI Governance Clients
    MailSvc -->|gRPC| AiGovSvc
    OcrSvc -->|gRPC| AiGovSvc
    ComplianceSvc -->|gRPC| AiGovSvc
    RouteSvc -->|gRPC| AiGovSvc
    NegotiationSvc -->|gRPC| AiGovSvc
    AssistantSvc -->|gRPC| AiGovSvc
    DevOpsSvc -->|gRPC| AiGovSvc

    %% Event Bus Connections
    ShipmentSvc -.->|Outbox CDC| RabbitMQ
    RouteSvc -.->|Outbox CDC| RabbitMQ
    MailSvc -.->|Outbox CDC| RabbitMQ
    OcrSvc -.->|Outbox CDC| RabbitMQ
    ComplianceSvc -.->|Outbox CDC| RabbitMQ
    GpsSvc -.->|Outbox CDC| RabbitMQ
    IamSvc -.->|Outbox CDC| RabbitMQ
    BillingSvc -.->|Publish Event| RabbitMQ
    NegotiationSvc -.->|Publish Event| RabbitMQ
    AiGovSvc -.->|Outbox CDC| RabbitMQ
    DevOpsSvc -.->|Outbox CDC| RabbitMQ

    RabbitMQ -.->|Consume| NotificationSvc
    RabbitMQ -.->|Consume| RealtimeHub
    RabbitMQ -.->|Consume| BillingSvc
    RabbitMQ -.->|Consume| DocumentOcr
    RabbitMQ -.->|Consume| RegulatoryCompliance
    RabbitMQ -.->|Consume| GpsSvc
    RabbitMQ -.->|Consume| MailSvc
```

---

## 2. End-to-End Synchronous gRPC Sequence

The sequence diagram below illustrates synchronous orchestration with security metadata propagation:

```mermaid
sequenceDiagram
    autonumber
    actor User as Staff User / Browser
    participant Gateway as YARP API Gateway
    participant BFF as Staff.Bff
    participant AuthFilter as RequirePermissionFilter
    participant Redis as Redis Cache
    participant ShipmentService as ShipmentWorkflow (.NET)
    participant DB as PostgreSQL (Shipment DB)

    User->>Gateway: POST /api/v1/shipments (Bearer JWT Cookie)
    Gateway->>BFF: Forward request to Staff.Bff
    BFF->>AuthFilter: Validate JWT & Required Permission [shipments:create]
    AuthFilter->>Redis: Fetch cached user permissions (if not in JWT)
    Redis-->>AuthFilter: Return active permissions
    AuthFilter-->>BFF: Authorization Approved

    Note over BFF,ShipmentService: Construct gRPC Call with Headers:<br/>x-tenant-id: 9a3c...<br/>x-user-id: 4f1a...<br/>x-roles: OPERATOR<br/>x-permissions: shipments:create,shipments:read

    BFF->>ShipmentService: gRPC CreateShipment(CreateShipmentRequest)
    ShipmentService->>ShipmentService: AuthInterceptor extracts ICurrentUserService
    ShipmentService->>ShipmentService: Validate Business Rules & State Machine
    ShipmentService->>DB: INSERT INTO shipments (...) + INSERT INTO outbox_messages (...)
    DB-->>ShipmentService: Transaction Committed
    ShipmentService-->>BFF: Return ShipmentDto Response
    BFF-->>Gateway: HTTP 201 Created (JSON)
    Gateway-->>User: HTTP 201 Created
```

---

## 3. Asynchronous Event-Driven Flow (Transactional Outbox Pattern)

The asynchronous event processing architecture guarantees zero message loss during database updates:

```mermaid
sequenceDiagram
    autonumber
    participant ShipmentApp as ShipmentWorkflow Service
    participant DB as PostgreSQL (shipment_workflow)
    participant OutboxWorker as OutboxProcessorBackgroundService
    participant Rabbit as RabbitMQ (Exchange: shipment.contracts.events)
    participant OcrService as DocumentOcr Service
    participant ComplianceService as RegulatoryCompliance Service
    participant RealtimeHub as RealtimeHub (WebSocket)
    actor Browser as Frontend Web Client

    Note over ShipmentApp,DB: User attaches Document & submits shipment
    ShipmentApp->>DB: Begin DB Transaction
    ShipmentApp->>DB: Update Shipment Status = SUBMITTED
    ShipmentApp->>DB: Insert DocumentAttachedEvent into outbox_messages
    ShipmentApp->>DB: Insert ShipmentSubmittedEvent into outbox_messages
    ShipmentApp->>DB: Commit DB Transaction

    loop Every 2000ms
        OutboxWorker->>DB: SELECT * FROM outbox_messages WHERE processed_at IS NULL FOR UPDATE SKIP LOCKED
        DB-->>OutboxWorker: Return pending event batch
        OutboxWorker->>Rabbit: Publish DocumentAttachedEvent
        OutboxWorker->>Rabbit: Publish ShipmentSubmittedEvent
        OutboxWorker->>DB: UPDATE outbox_messages SET processed_at = NOW()
    end

    par Parallel Event Consumption
        Rabbit-->>OcrService: Deliver DocumentAttachedEvent
        OcrService->>OcrService: Ingest document into OCR queue
    and
        Rabbit-->>ComplianceService: Deliver ShipmentSubmittedEvent
        ComplianceService->>ComplianceService: Trigger automated Trade Regulation Evaluation
    and
        Rabbit-->>RealtimeHub: Deliver ShipmentSubmittedEvent
        RealtimeHub->>Browser: Push WebSocket message to room 'shipment:{id}'
    end
```

---

## 4. AI Governance Gateway & Model Routing Boundary

```mermaid
graph LR
    subgraph ConsumerServices ["Consumer Services"]
        MailSvc["MailService"]
        OcrSvc["DocumentOcr"]
        CompSvc["RegulatoryCompliance"]
        RouteSvc["RoutePlanningAgent"]
        NegoSvc["negotiation-agent"]
        AssistSvc["customer-assistant"]
        DevOpsSvc["devops-agent"]
    end

    subgraph AiGovernance ["AI Governance Service Boundary (Java 21)"]
        AiGateway["gRPC AiExecutionService / AiGovernanceService"]
        PolicyEngine["Policy & Rate Limiting Engine"]
        TokenBucket["Redis Lua Rate Limiter (Token Bucket)"]
        QuotaEnforcer["Tenant Quota & Budget Enforcer"]
        AuditLedger["PostgreSQL Audit & Decision Ledger"]
        Router["Dynamic Provider Router (Shared Pool vs BYOK)"]
    end

    subgraph UpstreamProviders ["Foundation Model Providers"]
        OpenAI["Azure OpenAI / OpenAI API<br/>(GPT-4o, text-embedding-3)"]
        Anthropic["Anthropic API<br/>(Claude 3.5 Sonnet)"]
        Gemini["Google Gemini API<br/>(Gemini 1.5 Flash / Pro)"]
    end

    ConsumerServices -->|gRPC Generate / Embed with x-tenant-id & capability_code| AiGateway
    AiGateway --> PolicyEngine
    PolicyEngine --> TokenBucket
    PolicyEngine --> QuotaEnforcer
    PolicyEngine --> AuditLedger
    PolicyEngine --> Router

    Router -->|Shared Pool Key / Tenant BYOK| OpenAI
    Router -->|Shared Pool Key / Tenant BYOK| Anthropic
    Router -->|Shared Pool Key / Tenant BYOK| Gemini

    AuditLedger -.->|ai.usage.tracked| RabbitMQBus["RabbitMQ (Usage Metering)"]
```

---

## 5. Database Ownership & Storage Architecture

Aurora adheres strictly to the **Database-per-Service** architectural pattern. No service shares database instances or tables directly with another service.

```mermaid
graph TD
    subgraph DataPerimeters ["Database Perimeters"]
        subgraph PostgresInstance ["PostgreSQL 16 Cluster"]
            DB_IAM[("iam_tenant<br/>(Users, Roles, Perms, Tenants)")]
            DB_MAIL[("mail_service<br/>(Domains, Mailboxes, Drafts, Threads)")]
            DB_SHIPMENT[("shipment_workflow<br/>(Shipments, Cargo, Documents, Milestones)")]
            DB_ROUTE[("route_planning<br/>(Routes, Stops, Risk Policies, Approvals)")]
            DB_OCR[("document_ocr<br/>(OCR Jobs, Provider Attempts)")]
            DB_COMPLIANCE[("regulatory_compliance<br/>(pgvector Regulations, Knowledge, Findings)")]
            DB_GPS[("gps_tracking<br/>(GPS Positions, Geofences, Alerts)")]
            DB_NOTIF[("notification<br/>(Messages, User Preferences, Delivery Attempts)")]
            DB_AIGOV[("ai_governance<br/>(Policies, Decisions, Quotas, Usage Records)")]
            DB_DEVOPS[("devops_agent<br/>(Incidents, Rules, Remediation Actions)")]
            DB_BILLING[("billing_service<br/>(Invoices, Items, Wallets, Escrow)")]
            DB_FINANCIAL[("financial_service<br/>(Tax Rates, Customs Tariffs, Exchange Rates)")]
            DB_NEGO[("negotiation_service<br/>(Sessions, Concession Rounds)")]
            DB_ASSIST[("customer_assistant<br/>(Conversations, Chat History)")]
        end

        subgraph RedisStorage ["Redis 7 Cluster"]
            Redis_BFF[("BFF Sessions & Rate Limits")]
            Redis_RateLimit[("AI Capacity Lua Reservation")]
            Redis_MailClaim[("Mail Thread Claim Locks")]
            Redis_WsRooms[("Socket.IO Redis Adapter")]
        end

        subgraph ObjectStorage ["Object & Mail Storage"]
            R2_MIME[("Cloudflare R2 / S3<br/>(MIME Emails, Attachments, OCR PDFs)")]
            Stalwart_Store[("Stalwart Mail Engine<br/>(JMAP / IMAP / Maildirs)")]
        end
    end
```

---

## 6. Security & Permission Evaluation Pipeline

```mermaid
flowchart TD
    Req([HTTP Request from Client]) --> YARP[YARP Gateway]
    YARP --> BFF[BFF Endpoint]
    BFF --> AuthFilter{RequirePermission Attribute}
    
    AuthFilter -- Missing Bearer / Cookie --> Res401[401 Unauthorized]
    AuthFilter -- System Admin Role Present --> Allow[Bypass Permission Check]
    
    AuthFilter -- Standard User --> CheckPerm{Has Required Capability?}
    CheckPerm -- No --> CheckLegacy{Has Deprecated Fallback?}
    CheckLegacy -- No --> Res403[403 Forbidden]
    CheckLegacy -- Yes (Audit Logged) --> Allow
    CheckPerm -- Yes --> Allow
    
    Allow --> MetadataBuilder[Build gRPC Call Metadata Headers]
    MetadataBuilder -->|x-tenant-id, x-user-id, x-roles, x-permissions| GrpcClient[Execute gRPC Client]
    
    GrpcClient --> GrpcServer[Downstream Service gRPC Method]
    GrpcServer --> GrpcInterceptor[AuthInterceptor]
    GrpcInterceptor --> TenantContext[Establish Scoped ICurrentUserService]
    TenantContext --> EfQuery[EF Core Global Tenant Query Filter]
    EfQuery --> DB[(Service Database)]
```
