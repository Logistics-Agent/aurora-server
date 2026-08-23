#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Disaster Recovery & Volume Restore
# Gate 6G: OPERATIONS-READY
# ==============================================================================
set -euo pipefail

ARCHIVE_PATH="${1:-}"

if [[ -z "${ARCHIVE_PATH}" || ! -f "${ARCHIVE_PATH}" ]]; then
    echo "[-] Error: Please specify the path to a valid backup archive (.tar.gz)." >&2
    echo "    Usage: $0 /opt/aurora/mail/backups/aurora-mail-backup-YYYYMMDD_HHMMSS.tar.gz" >&2
    exit 1
fi

echo "======================================================================"
echo ">> Starting Disaster Recovery Restore from: ${ARCHIVE_PATH}"
echo "======================================================================"

# 1. Verify Checksum if present
if [[ -f "${ARCHIVE_PATH}.sha256" ]]; then
    echo "[+] Verifying SHA256 checksum..."
    cd "$(dirname "${ARCHIVE_PATH}")"
    sha256sum -c "$(basename "${ARCHIVE_PATH}.sha256")"
fi

TEMP_RESTORE_DIR=$(mktemp -d /tmp/aurora-restore.XXXXXX)
trap 'rm -rf "${TEMP_RESTORE_DIR}"' EXIT

# 2. Extract Archive
echo "[+] Extracting backup archive..."
tar -xzf "${ARCHIVE_PATH}" -C "${TEMP_RESTORE_DIR}"
EXTRACTED_SUBDIR=$(find "${TEMP_RESTORE_DIR}" -mindepth 1 -maxdepth 1 -type d | head -n1)

# 3. Stop Mail Services before volume restore
echo "[+] Stopping running containers..."
COMPOSE_FILE="/opt/aurora/mail/docker-compose.prod.yml"
if [[ -f "${COMPOSE_FILE}" ]]; then
    docker compose -f "${COMPOSE_FILE}" stop || true
fi

# 4. Restore Stalwart Volume
if [[ -f "${EXTRACTED_SUBDIR}/stalwart_data.tar.gz" ]]; then
    echo "[+] Restoring Stalwart volume..."
    docker run --rm \
        -v stalwart_data:/data \
        -v "${EXTRACTED_SUBDIR}":/backup \
        alpine sh -c "rm -rf /data/* && tar -xzf /backup/stalwart_data.tar.gz -C /data"
fi

# 5. Restore Redis Volume
if [[ -f "${EXTRACTED_SUBDIR}/redis_data.tar.gz" ]]; then
    echo "[+] Restoring Redis volume..."
    docker run --rm \
        -v redis_data:/data \
        -v "${EXTRACTED_SUBDIR}":/backup \
        alpine sh -c "rm -rf /data/* && tar -xzf /backup/redis_data.tar.gz -C /data"
fi

# 6. Restore RabbitMQ Volume
if [[ -f "${EXTRACTED_SUBDIR}/rabbitmq_data.tar.gz" ]]; then
    echo "[+] Restoring RabbitMQ volume..."
    docker run --rm \
        -v rabbitmq_data:/data \
        -v "${EXTRACTED_SUBDIR}":/backup \
        alpine sh -c "rm -rf /data/* && tar -xzf /backup/rabbitmq_data.tar.gz -C /data"
fi

# 7. Restart Stack
echo "[+] Restarting Aurora Mail containers..."
if [[ -f "${COMPOSE_FILE}" ]]; then
    docker compose -f "${COMPOSE_FILE}" up -d
fi

echo "======================================================================"
echo "[✓] Disaster Recovery Restoration completed successfully."
echo "======================================================================"
