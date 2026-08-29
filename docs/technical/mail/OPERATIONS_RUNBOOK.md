# Aurora Mail Platform — Production Operations Runbook

> **Status**: AUTHORITATIVE / SRE PRODUCTION RUNBOOK (CODE-FIRST)  
> **Source-of-Truth**: Audited against `deploy/scripts/`, `deploy/RUNBOOK.md`, `monitor-resources.sh`, `backup.sh`, `restore.sh`, and `production-smoke-test.sh`.

---

## 1. Routine Operational Commands

### 1.1 Checking Stack Health & Status
```bash
# Check Docker container states
docker compose -f /opt/aurora/mail/docker-compose.prod.yml ps

# Check MailService health endpoints
curl -s http://127.0.0.1:9090/health/live
curl -s http://127.0.0.1:9090/health/ready

# Check Stalwart mail server health
curl -s http://127.0.0.1:8080/healthz

# Run system resource monitor script
bash /opt/aurora/mail/scripts/monitor-resources.sh
```

### 1.2 Viewing Logs
```bash
# Follow MailService application logs
docker compose -f /opt/aurora/mail/docker-compose.prod.yml logs -f --tail=100 mail-service

# Follow Stalwart mail server logs
docker compose -f /opt/aurora/mail/docker-compose.prod.yml logs -f --tail=100 stalwart

# Follow ClamAV antivirus logs
docker compose -f /opt/aurora/mail/docker-compose.prod.yml logs -f --tail=50 clamav
```

---

## 2. Backup & Disaster Recovery

### 2.1 Automated Daily Backups (`backup.sh`)
Cron executes daily at `02:00 UTC`:
```bash
sudo bash /opt/aurora/mail/scripts/backup.sh
```
- Creates gzipped tarball archive under `/opt/aurora/mail/backups/backup_YYYYMMDD_HHMMSS.tar.gz`.
- Backs up:
  - Stalwart data & blob volumes
  - RabbitMQ definitions and queue state
  - Redis append-only file (AOF)
  - TLS certificate directory (`/opt/aurora/mail/certs`)
  - Environment configuration (`.env.prod`)
- Enforces a **14-day rolling retention policy** (automatically prunes older archives).

### 2.2 Restoring from Backup (`restore.sh`)
```bash
sudo bash /opt/aurora/mail/scripts/restore.sh /opt/aurora/mail/backups/backup_20260828_020000.tar.gz
```
- Stops running containers safely.
- Unpacks volume data and restores state.
- Restarts services and runs health verifications.

---

## 3. Incident Response & Troubleshooting

### Scenario A: Stalwart Fails Healthcheck / Port 25 Inaccessible
1. Check if another process is occupying Port 25: `sudo lsof -i :25`.
2. Inspect Stalwart logs: `docker logs aurora-stalwart --tail=100`.
3. Check certificate expiry: `openssl x509 -enddate -noout -in /opt/aurora/mail/certs/fullchain.pem`.
4. Restart Stalwart: `docker compose -f /opt/aurora/mail/docker-compose.prod.yml restart stalwart`.

### Scenario B: ClamAV Daemon Socket Connection Refused
1. Check memory usage: `free -m` (ClamAV requires ~1.2 GB RAM for signature database).
2. Verify ClamAV container health: `docker inspect aurora-clamav | grep -A 5 Health`.
3. Force signature reload: `docker compose -f /opt/aurora/mail/docker-compose.prod.yml restart clamav`.

### Scenario C: RabbitMQ Queue Backpressure / Outbox Stalling
1. Check pending outbox messages in Neon DB:
   ```sql
   SELECT COUNT(*), MIN(created_at) FROM outbox_messages WHERE processed_at IS NULL;
   ```
2. Check RabbitMQ connection status from MailService logs.
3. Access RabbitMQ management UI via SSH tunnel (`localhost:15672`) to check queue depths and unacknowledged messages.

### Scenario D: Dead-Letter Queue Recovery
When an email encounters a permanent failure (e.g. unresolvable external DNS or corrupted attachment stream), it transitions to `PipelineStatus.DeadLettered`.
- System Admin inspects dead-lettered messages in `System.Bff`.
- Triggers requeue via:
  ```http
  POST /api/v1/system/mail/dead-letter/{id}/requeue
  ```
- MailService resets retry counters and re-submits the message to the pipeline runner.

---

## 4. End-to-End Production Smoke Test (`production-smoke-test.sh`)

Validate end-to-end functionality across SMTP, IMAP, gRPC, and database:
```bash
bash /opt/aurora/mail/scripts/production-smoke-test.sh
```
- Tests SMTP connection handshake on port 25/587.
- Submits test inbound payload.
- Verifies thread creation in database.
- Verifies ClamAV virus detection using standard EICAR test string.
