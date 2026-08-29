# Aurora Mail Platform — Production Deployment & Infrastructure

> **Status**: AUTHORITATIVE / PRODUCTION ARCHITECTURE (CODE-FIRST)  
> **Source-of-Truth**: Audited against `deploy/docker-compose.prod.yml`, `deploy/.env.prod.example`, `deploy/scripts/setup-host.sh`, `deploy/scripts/deploy.sh`, `deploy/scripts/rollback.sh`, and `deploy/scripts/configure-tls.sh`.

---

## 1. Production Topology & Architecture

The production Mail node runs on a dedicated host (e.g. Ubuntu 24.04 LTS / Mini PC) isolated within the production VPC:

```
                  ┌───────────────────────────────────────────────┐
                  │          Production Host (Ubuntu 24.04)       │
                  │                                               │
  Internet ───────┼──> Port 25: Stalwart SMTP                     │
  Internet ───────┼──> Port 587: Stalwart Submission (TLS)        │
  Internet ───────┼──> Port 993: Stalwart IMAPS                   │
                  │                                               │
                  │   ┌───────────────────────────────────────┐   │
  API Gateway ────┼──>│ Port 5003: MailService (gRPC Internal)│   │
  Scraper (Local) ┼──>│ Port 9090: Health & OTel Metrics      │   │
                  │   └───────────────────────────────────────┘   │
                  │                                               │
                  │   Internal Docker Network (mail_internal):    │
                  │   ├── redis:6379 (Claims & Rate Limits)       │
                  │   ├── rabbitmq:5672 (Domain Events)           │
                  │   ├── clamav:3310 (Antivirus Daemon)          │
                  │   ├── spamassassin:783 (Spam Filter)          │
                  │   └── stalwart:8080 (Loopback Admin API)      │
                  └───────────────────────┬───────────────────────┘
                                          │
                     Managed Cloud External Connections:
                     ├── Neon PostgreSQL 16 (SSL Required)
                     ├── Cloudflare R2 (S3 API - Raw EMLs)
                     └── Central Java AiGovernance (gRPC)
```

---

## 2. Host Prerequisites & Firewall Setup (`setup-host.sh`)

Execute initial host provisioning:
```bash
sudo bash /opt/aurora/mail/scripts/setup-host.sh
```

### 2.1 UFW Firewall Rules
| Port / Protocol | Source | Direction | Action | Description |
|---|---|---|---|---|
| `22/tcp` | Admin IP / VPN | Inbound | `ALLOW` | SSH remote management |
| `25/tcp` | `0.0.0.0/0` | Inbound | `ALLOW` | Internet inbound SMTP |
| `587/tcp` | `0.0.0.0/0` | Inbound | `ALLOW` | Authenticated submission |
| `993/tcp` | `0.0.0.0/0` | Inbound | `ALLOW` | Encrypted IMAP |
| `5003/tcp` | VPC Gateway / BFFs | Inbound | `ALLOW` | Internal gRPC service |
| `8080/tcp` | `127.0.0.1` | Loopback | `ALLOW` | Stalwart Management REST |
| `9090/tcp` | `127.0.0.1` | Loopback | `ALLOW` | Prometheus metrics & health |

---

## 3. Automated Deployment & Rollback (`deploy.sh` & `rollback.sh`)

### 3.1 Zero-Downtime Deployment Flow (`deploy.sh`)
```mermaid
sequenceDiagram
    autonumber
    actor CI as CI/CD Pipeline
    participant Host as Production Host (deploy.sh)
    participant DB as Neon PostgreSQL
    participant Docker as Docker Compose

    CI->>Host: Execute deploy.sh with IMAGE_TAG
    Host->>Host: Verify .env.prod secrets & TLS cert validity
    Host->>DB: Execute efbundle (Apply additive EF Core migrations)
    DB-->>Host: Migration Success
    Host->>Docker: docker compose pull mail-service
    Host->>Docker: docker compose up -d --no-deps mail-service
    Host->>Host: Poll /health/ready endpoint for 30s
    alt Health Check Passes (200 OK)
        Host-->>CI: Deployment SUCCESS
    else Health Check Fails (Timeout / Crash)
        Host->>Host: Trigger auto-rollback (rollback.sh)
        Host->>Docker: Revert container to PREVIOUS_IMAGE_TAG
        Host-->>CI: Deployment FAILED (Rolled back)
    end
```

### 3.2 Executing Deployment
```bash
export MAIL_SERVICE_IMAGE="ghcr.io/aurora/mail-service:1.2.0"
sudo bash /opt/aurora/mail/scripts/deploy.sh
```

### 3.3 Manual Rollback
```bash
sudo bash /opt/aurora/mail/scripts/rollback.sh
```

---

## 4. TLS Certificate Management (`configure-tls.sh`)

Aurora utilizes **Let's Encrypt Wildcard Certificates** issued via Cloudflare DNS-01 challenge:
```bash
sudo bash /opt/aurora/mail/scripts/configure-tls.sh mail.aurora-logistics.com
```
- Certificate files are written to `/opt/aurora/mail/certs/`:
  - `fullchain.pem` (chmod 644)
  - `privkey.pem` (chmod 600)
- Auto-renewed via `certbot` systemd timer every 60 days.

---

## 5. Environment Configuration Template (`.env.prod`)

```ini
# Production Environment Variables (.env.prod)
ASPNETCORE_ENVIRONMENT=Production
MAIL_SERVICE_IMAGE=ghcr.io/aurora/mail-service:1.0.0

# Neon PostgreSQL (Managed Cloud SSL)
NEON_DATABASE_URL=postgres://aurora_mail:SECRET@ep-cool-fog-123.ap-southeast-1.aws.neon.tech/mail_prod?sslmode=require

# External Cloudflare R2
CF_ACCOUNT_ID=d9a8c7e6b5a4...
R2_BUCKET_NAME=aurora-mail-production
R2_ACCESS_KEY=...
R2_SECRET_KEY=...

# Central AI Governance Service
AI_GOVERNANCE_GRPC_ENDPOINT=http://ai-gov.internal.aurora-logistics.com:50051

# Local RabbitMQ
RABBITMQ_USER=aurora
RABBITMQ_PASSWORD=SecureRabbitPassword123!
RABBITMQ_VHOST=mail

# Stalwart Admin API Secret
STALWART_ADMIN_TOKEN=StalwartSuperAdminSecretToken123!
```
