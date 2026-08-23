#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Mini PC Health & Resource Monitor
# Gate 6G: OPERATIONS-READY
# ==============================================================================
set -euo pipefail

DISK_THRESHOLD=85
RAM_THRESHOLD=90

echo "======================================================================"
echo ">> Aurora Mail Platform Resource Observation (Host: $(hostname))"
echo "   Timestamp: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "======================================================================"

# 1. Disk Utilization Check
echo -e "\n--- 1. Disk Utilization ---"
DISK_USAGE=$(df -h / | awk 'NR==2 {print $5}' | sed 's/%//')
echo "Root Partition Usage: ${DISK_USAGE}%"
if [[ "${DISK_USAGE}" -ge "${DISK_THRESHOLD}" ]]; then
    echo -e "[\e[31mALERT\e[0m] Disk usage is above threshold (${DISK_THRESHOLD}%). Clean up logs or expand disk!"
else
    echo -e "[\e[32mOK\e[0m] Disk headroom is healthy."
fi

# 2. RAM Memory Check
echo -e "\n--- 2. RAM Memory Utilization ---"
if command -v free >/dev/null 2>&1; then
    free -h
fi

# 3. Docker Container Health & Resource Snapshot
echo -e "\n--- 3. Container Resource Consumption (docker stats snapshot) ---"
if command -v docker >/dev/null 2>&1; then
    docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}\t{{.NetIO}}\t{{.BlockIO}}"
fi

# 4. Check Health Probes
echo -e "\n--- 4. Local Health Check Verification ---"
if curl -s --max-time 3 http://localhost:9090/health/live | grep -q "Healthy"; then
    echo -e "[\e[32mPASS\e[0m] MailService Liveness:  HEALTHY"
else
    echo -e "[\e[31mFAIL\e[0m] MailService Liveness:  UNHEALTHY / DOWN"
fi

if curl -s --max-time 3 http://localhost:9090/health/ready | grep -q "Healthy"; then
    echo -e "[\e[32mPASS\e[0m] MailService Readiness: HEALTHY"
else
    echo -e "[\e[33mWARN\e[0m] MailService Readiness: DEGRADED or UNHEALTHY"
fi

if curl -s --max-time 3 http://localhost:8080/healthz >/dev/null 2>&1; then
    echo -e "[\e[32mPASS\e[0m] Stalwart Management:   HEALTHY"
else
    echo -e "[\e[31mFAIL\e[0m] Stalwart Management:   UNREACHABLE"
fi

echo "======================================================================"
