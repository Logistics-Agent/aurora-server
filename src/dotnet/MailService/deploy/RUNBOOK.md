# Aurora Mail Platform — Production Operations Runbook

This document is the authoritative operations guide for managing, monitoring, updating, and troubleshooting the **Aurora Mail Platform** running on the dedicated production **Mini PC (Ubuntu 24.04 LTS)**.

---

## 1. Architecture & Ownership Boundaries

```text
[ External Clients / Internet Senders ]
                 │
       (Port 25, 587, 993)
                 ▼
        ┌─────────────────┐
        │     Stalwart    │ <──────┐
        │   Mail Server   │        │
        └────────┬────────┘        │
                 │                 │
                 ▼                 │
        ┌─────────────────┐        │
        │   MailService   │ ───────┘ (SMTP Delivery)
        │  (Port 5003/9090)
        └────────┬────────┘
                 │
   ┌─────────────┼─────────────┬──────────────┬───────────────┐
   │             │             │              │               │
   ▼             ▼             ▼              ▼               ▼
[Redis]      [RabbitMQ]     [ClamAV]   [SpamAssassin]   [External Aurora]
(Rate Limit)  (Events)      (Malware)      (Spam)        - Neon PostgreSQL (Managed SSL)
                                                         - AI Governance gRPC (AKS/VPC)
                                                         - Cloudflare R2 (EML Storage)
                                                         - OpenTelemetry OTLP (AKS Collector)
```

### Disaster Recovery & Storage Ownership

| Component | Location | Ownership & Backup Strategy | RPO Target | RTO Target |
|---|---|---|---|---|
| **Neon PostgreSQL** | Managed Cloud | Neon automated Point-in-Time Recovery (PITR) + Daily Branch Snapshots | 5 minutes | 15 minutes |
| **Cloudflare R2** | Managed Cloud | Cloudflare multi-AZ distributed storage + Object Versioning & Immutable Lifecycle | 0 minutes | 5 minutes |
| **Stalwart Data** | Mini PC SSD | Daily hot tarball backup via `backup.sh` (14-day rolling retention) | 24 hours | 30 minutes |
| **RabbitMQ State** | Mini PC SSD | Daily definitions export + queue data volume snapshot | 24 hours | 15 minutes |
| **Redis Cache** | Mini PC SSD | AOF Append-Only persistence + daily backup | 1 hour | 10 minutes |

---

## 2. Directory Layout on the Mini PC

```text
/opt/aurora/mail/
├── .env.prod                     # Production environment secrets (chmod 600)
├── docker-compose.prod.yml       # Production container topology
├── bin/
│   └── efbundle                  # Self-contained Linux x64 EF Core migration executable
├── certs/
│   ├── fullchain.pem             # Let's Encrypt TLS certificate
│   └── privkey.pem               # Let's Encrypt TLS private key (chmod 600)
├── config/
│   ├── cloudflare.ini            # Scoped Cloudflare DNS API Token for DNS-01 (chmod 600)
│   └── stalwart/                 # Stalwart server configuration
├── data/                         # Persistent volumes
│   ├── stalwart/
│   ├── rabbitmq/
│   ├── redis/
│   └── clamav/
├── backups/                      # Gzipped rolling backup archives (.tar.gz)
├── logs/                         # Rotated application & service logs
└── scripts/
    ├── setup-host.sh             # Initial OS & UFW firewall setup
    ├── deploy.sh                 # Health-gated deployment runner with auto-rollback
    ├── rollback.sh               # Emergency application rollback runner
    ├── backup.sh                 # Daily automated backup script
    ├── restore.sh                # Disaster recovery volume restoration
    ├── configure-tls.sh          # Cloudflare DNS-01 automated TLS certificate issuance
    ├── verify-dns-deliverability.sh # Pre-flight DNSBL/SPF/DKIM/PTR verifier
    ├── monitor-resources.sh      # Resource & health status monitor
    └── production-smoke-test.sh  # End-to-end integration test runner
```

---

## 3. Database Migration Compatibility Policy

To guarantee safe zero-risk deployments and smooth application rollbacks, all EF Core migrations must strictly adhere to the **Expand and Contract pattern**:

### Allowed Automatic Release Migration Patterns:
1. `ADD Table`: Creating new tables.
2. `ADD Column (Nullable)`: Adding optional columns.
3. `ADD Column (Non-Nullable with default)`: Adding columns with backward-compatible defaults.
4. `ADD Index / Constraint`: Creating indexes or non-breaking foreign keys.

### Prohibited without Multi-Phase Staged Deployment:
1. `DROP Column / Table`: Prohibited in the same release as code changes.
2. `RENAME Column`: Must be broken into: Add new column -> Dual-write -> Backfill -> Read new column -> Drop old column.
3. `Incompatible Type Changes`: Must be staged.

> [!IMPORTANT]
> **Rollback Invariant**: When an application rollback occurs, the database schema is **NOT** automatically downgraded. The previous application image must remain 100% compatible with the newly applied additive schema.

---

## 4. Standard Operating Procedures (SOP)

### 4.1 First-Time Server Provisioning (Ubuntu 24.04 LTS)
```bash
# 1. Clone repository deployment bundle
sudo mkdir -p /opt/aurora/mail
sudo cp -r src/dotnet/MailService/deploy/* /opt/aurora/mail/

# 2. Run Host Setup Script (Specify SSH subnet CIDR if available)
sudo bash /opt/aurora/mail/scripts/setup-host.sh "192.168.1.0/24"

# 3. Create .env.prod from template
sudo cp /opt/aurora/mail/.env.prod.example /opt/aurora/mail/.env.prod
sudo nano /opt/aurora/mail/.env.prod
sudo chmod 600 /opt/aurora/mail/.env.prod

# 4. Configure Cloudflare DNS-01 API Token
sudo cat << 'EOF' > /opt/aurora/mail/config/cloudflare.ini
dns_cloudflare_api_token = YOUR_SCOPED_CLOUDFLARE_ZONE_DNS_EDIT_TOKEN
EOF
sudo chmod 600 /opt/aurora/mail/config/cloudflare.ini

# 5. Provision TLS Certificates via Cloudflare DNS-01
sudo bash /opt/aurora/mail/scripts/configure-tls.sh aurora.vn mail.aurora.vn

# 6. Run Pre-Flight Deliverability Check
sudo bash /opt/aurora/mail/scripts/verify-dns-deliverability.sh aurora.vn mail.aurora.vn aurora

# 7. Execute Initial Deployment
sudo bash /opt/aurora/mail/scripts/deploy.sh
```

---

### 4.2 Application Updates
```bash
# Triggered automatically via GitHub Actions CI/CD, or manually on the Mini PC:
cd /opt/aurora/mail
sudo bash scripts/deploy.sh
```

---

### 4.3 Emergency Rollback
```bash
cd /opt/aurora/mail
sudo bash scripts/rollback.sh
```

---

### 4.4 Automated Daily Backups
The automated backup script is scheduled via cron:
```bash
# Daily at 02:00 UTC
0 2 * * * /opt/aurora/mail/scripts/backup.sh >> /opt/aurora/mail/logs/backup.log 2>&1
```

---

### 4.5 Restoring from Disaster Recovery Backup
```bash
sudo bash /opt/aurora/mail/scripts/restore.sh /opt/aurora/mail/backups/aurora-mail-backup-20260822_120000.tar.gz
```

---

## 5. Troubleshooting & Incident Response

### Incident 1: MailService reports `Unhealthy` on `/health/ready`
1. Query health details:
   ```bash
   curl -s http://localhost:9090/health | jq
   ```
2. Diagnose failing dependency:
   - If `neon-postgres` fails: Verify Internet connectivity to Neon PostgreSQL and SSL certificate validity.
   - If `stalwart` fails: `docker logs aurora-stalwart --tail 50`
   - If `clamav` fails: Verify if virus definition signature update is in progress (`docker logs aurora-mail-clamav`).

### Incident 2: Inbound Emails Failing or Quarantined
1. Query quarantine records via gRPC management API or Neon database:
   ```sql
   SELECT id, message_id, quarantine_reason, status, quarantined_at
   FROM quarantine_records ORDER BY quarantined_at DESC LIMIT 10;
   ```
2. Inspect security check results:
   ```sql
   SELECT stage, result, detail_json, duration_ms
   FROM security_check_results WHERE processed_message_id = '...' ORDER BY id ASC;
   ```

### Incident 3: Outbound Mails Marked as Spam by Gmail / Microsoft
1. Run deliverability verification:
   ```bash
   sudo bash /opt/aurora/mail/scripts/verify-dns-deliverability.sh aurora.vn mail.aurora.vn aurora
   ```
2. Verify PTR record with ISP:
   ```bash
   dig +short -x $(curl -s https://api.ipify.org)
   ```
