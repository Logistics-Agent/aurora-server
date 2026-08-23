#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Emergency Application Rollback Script
# Target OS: Ubuntu 24.04 LTS (linux-x64)
# Gate 6H: RELEASE-READY
# ==============================================================================
set -euo pipefail

DEPLOY_DIR="/opt/aurora/mail"
COMPOSE_FILE="${DEPLOY_DIR}/docker-compose.prod.yml"
ENV_FILE="${DEPLOY_DIR}/.env.prod"
ENV_PREV="${DEPLOY_DIR}/.env.prod.previous"

echo "======================================================================"
echo ">> Initiating Emergency Application Rollback"
echo "   Timestamp: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "======================================================================"

if [[ -f "${ENV_PREV}" ]]; then
    echo "[+] Restoring previous verified environment configuration..."
    cp "${ENV_PREV}" "${ENV_FILE}"
fi

if [[ -f "${COMPOSE_FILE}" && -f "${ENV_FILE}" ]]; then
    echo "[+] Re-instantiating containers with previous verified image..."
    docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d --remove-orphans

    echo "[+] Waiting for container stabilization..."
    sleep 5

    if curl -s --max-time 3 http://localhost:9090/health/live | grep -q "Healthy"; then
        echo "[✓] Rollback successful. Service liveness restored."
    else
        echo "[!] Rollback completed but liveness check is unhealthy. Inspect logs immediately!"
    fi
else
    echo "[-] Error: Missing compose or environment configuration for rollback." >&2
    exit 1
fi
