#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Health-Gated Deployment with Automatic Rollback
# Target OS: Ubuntu 24.04 LTS (linux-x64)
# Gate 6H: RELEASE-READY
# ==============================================================================
set -euo pipefail

DEPLOY_DIR="/opt/aurora/mail"
COMPOSE_FILE="${DEPLOY_DIR}/docker-compose.prod.yml"
ENV_FILE="${DEPLOY_DIR}/.env.prod"
MIGRATION_BUNDLE="${DEPLOY_DIR}/bin/efbundle"

echo "======================================================================"
echo ">> Starting Health-Gated Production Deployment for Aurora Mail Platform"
echo "   Timestamp: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "======================================================================"

# 1. Pre-flight Validation
if [[ ! -f "${ENV_FILE}" ]]; then
    echo "[-] Error: Environment file ${ENV_FILE} not found!" >&2
    exit 1
fi

if [[ ! -f "${COMPOSE_FILE}" ]]; then
    echo "[-] Error: Compose file ${COMPOSE_FILE} not found!" >&2
    exit 1
fi

# Export environment variables from .env.prod
set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

# 2. Database Migration Step (Run against Neon BEFORE touching running containers)
echo "[+] Step 1/4: Applying backward-compatible EF Core migrations to Neon PostgreSQL..."
if [[ -f "${MIGRATION_BUNDLE}" ]]; then
    chmod +x "${MIGRATION_BUNDLE}"
    if ! "${MIGRATION_BUNDLE}" --connection "${NEON_DATABASE_URL}"; then
        echo "[-] Error: EF Core schema migration failed! Aborting deployment." >&2
        exit 1
    fi
    echo "[✓] Database schema migration successfully verified and applied."
else
    echo "[!] Notice: Migration bundle ${MIGRATION_BUNDLE} not found locally (assuming pre-applied via CI/CD)."
fi

# 3. Pull Pinned Production Container Images
echo "[+] Step 2/4: Pulling pinned container images..."
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" pull

# 4. Recreate/Update Stack
echo "[+] Step 3/4: Updating container instances..."
docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" up -d --remove-orphans

# 5. Post-Deployment Health-Gate Verification
echo "[+] Step 4/4: Waiting for service health convergence (up to 60s)..."
MAX_ATTEMPTS=20
ATTEMPT=1
READY=false

while [[ ${ATTEMPT} -le ${MAX_ATTEMPTS} ]]; do
    echo "    Attempt ${ATTEMPT}/${MAX_ATTEMPTS}: Probing /health/ready..."

    if curl -s --max-time 3 http://localhost:9090/health/ready | grep -q "Healthy"; then
        READY=true
        break
    fi

    sleep 3
    ((ATTEMPT++))
done

if [[ "${READY}" == "true" ]]; then
    echo "======================================================================"
    echo "[✓] Deployment SUCCEEDED! All services are Healthy and operational."
    echo "======================================================================"
    exit 0
else
    echo "======================================================================"
    echo "[-] Deployment FAILED health gate! Readiness probe failed."
    echo "    Initiating automatic application rollback..."
    echo "======================================================================"
    if [[ -f "${DEPLOY_DIR}/scripts/rollback.sh" ]]; then
        bash "${DEPLOY_DIR}/scripts/rollback.sh"
    fi
    exit 1
fi
