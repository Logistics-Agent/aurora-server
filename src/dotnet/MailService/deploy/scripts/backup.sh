#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Automated Daily Backup Script
# Gate 6G: OPERATIONS-READY
# ==============================================================================
set -euo pipefail

BACKUP_ROOT="/opt/aurora/mail/backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_DIR="${BACKUP_ROOT}/aurora-mail-backup-${TIMESTAMP}"
ARCHIVE_NAME="aurora-mail-backup-${TIMESTAMP}.tar.gz"

mkdir -p "${BACKUP_DIR}"
chmod 700 "${BACKUP_ROOT}" "${BACKUP_DIR}"

echo "======================================================================"
echo ">> Starting Aurora Mail Platform Backup: ${TIMESTAMP}"
echo "======================================================================"

# 1. Trigger Redis BGSAVE / sync
if docker ps --format '{{.Names}}' | grep -q "aurora-mail-redis"; then
    echo "[+] Forcing Redis AOF / DB save..."
    docker exec aurora-mail-redis redis-cli BGSAVE || true
    sleep 2
fi

# 2. Export RabbitMQ definitions
if docker ps --format '{{.Names}}' | grep -q "aurora-mail-rabbitmq"; then
    echo "[+] Exporting RabbitMQ definitions..."
    docker exec aurora-mail-rabbitmq rabbitmqctl export_definitions /tmp/rabbitmq_definitions.json 2>/dev/null || true
    docker cp aurora-mail-rabbitmq:/tmp/rabbitmq_definitions.json "${BACKUP_DIR}/rabbitmq_definitions.json" 2>/dev/null || true
fi

# 3. Backup Stalwart Data Volume
echo "[+] Backing up Stalwart mail storage..."
docker run --rm \
    -v stalwart_data:/data:ro \
    -v "${BACKUP_DIR}":/backup \
    alpine tar -czf /backup/stalwart_data.tar.gz -C /data .

# 4. Backup Redis Data Volume
echo "[+] Backing up Redis storage..."
docker run --rm \
    -v redis_data:/data:ro \
    -v "${BACKUP_DIR}":/backup \
    alpine tar -czf /backup/redis_data.tar.gz -C /data .

# 5. Backup RabbitMQ Data Volume
echo "[+] Backing up RabbitMQ storage..."
docker run --rm \
    -v rabbitmq_data:/data:ro \
    -v "${BACKUP_DIR}":/backup \
    alpine tar -czf /backup/rabbitmq_data.tar.gz -C /data .

# 6. Compress and Checksum full bundle
cd "${BACKUP_ROOT}"
tar -czf "${ARCHIVE_NAME}" -C "${BACKUP_ROOT}" "aurora-mail-backup-${TIMESTAMP}"
sha256sum "${ARCHIVE_NAME}" > "${ARCHIVE_NAME}.sha256"
rm -rf "${BACKUP_DIR}"

chmod 600 "${ARCHIVE_NAME}" "${ARCHIVE_NAME}.sha256"
echo "[+] Backup created: ${BACKUP_ROOT}/${ARCHIVE_NAME}"
echo "[+] Checksum: $(cat "${ARCHIVE_NAME}.sha256")"

# 7. Apply Retention Policy (Keep 14 daily backups)
echo "[+] Cleaning up local backups older than 14 days..."
find "${BACKUP_ROOT}" -name "aurora-mail-backup-*.tar.gz*" -mtime +14 -exec rm -f {} \;

echo "======================================================================"
echo "[✓] Backup completed successfully."
echo "======================================================================"
