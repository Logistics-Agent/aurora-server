#!/usr/bin/env bash
# =============================================================================
# Aurora Route Optimization — OSRM Map Dataset Downloader (Azure Blob Storage)
# =============================================================================
# Cross-platform / Linux / Kubernetes Init-Container script for downloading
# OSRM MLD dataset (.osrm*) from Azure Blob Storage into /data.
#
# Environment Variables:
#   OSRM_STORAGE_PROVIDER           - "AZURE_BLOB" (default)
#   AZURE_STORAGE_ACCOUNT_NAME      - Storage Account name (e.g. staurorarouteplanningdev)
#   AZURE_STORAGE_CONTAINER         - Blob container (default: osrm-data)
#   AZURE_OSRM_BLOB_PREFIX          - Blob prefix (default: "")
#   AZURE_STORAGE_CONNECTION_STRING - Connection string fallback (for local dev)
#   DESTINATION_DIR                 - Local download directory (default: ./data or /data)
#   FORCE_DOWNLOAD                  - Set to "true" to bypass local cache check
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DESTINATION="${DESTINATION_DIR:-${SCRIPT_DIR}/data}"
CONTAINER="${AZURE_STORAGE_CONTAINER:-osrm}"
PREFIX="${AZURE_OSRM_BLOB_PREFIX:-}"
FORCE="${FORCE_DOWNLOAD:-false}"

mkdir -p "${DESTINATION}"

echo "======================================================================"
echo ">> Aurora OSRM Dataset Downloader (Azure Blob Storage)"
echo "======================================================================"
echo "Storage Provider : ${OSRM_STORAGE_PROVIDER:-AZURE_BLOB}"
echo "Container        : ${CONTAINER}"
echo "Prefix           : ${PREFIX:-<root>}"
echo "Destination      : ${DESTINATION}"

# 1. Local Cache Check
if [ "${FORCE}" != "true" ] && [ -f "${DESTINATION}/map.osrm" ] && [ -f "${DESTINATION}/map.osrm.partition" ] && [ -f "${DESTINATION}/map.osrm.cells" ]; then
    FILE_COUNT=$(find "${DESTINATION}" -maxdepth 1 -name "map.osrm*" | wc -l)
    echo "[✓] OSRM MLD dataset already exists in volume (${FILE_COUNT} files found)."
    echo "    Found required files: map.osrm, map.osrm.partition, map.osrm.cells."
    echo "    Skipping download. Set FORCE_DOWNLOAD=true to re-download."
    exit 0
fi

# 2. Download via Azure CLI or AzCopy
if [ -n "${AZURE_STORAGE_CONNECTION_STRING:-}" ]; then
    echo "[+] Downloading via Azure Storage Connection String..."
    if [ -n "${PREFIX}" ]; then
        az storage blob download-batch \
            --source "${CONTAINER}" \
            --destination "${DESTINATION}" \
            --pattern "${PREFIX}/*" \
            --connection-string "${AZURE_STORAGE_CONNECTION_STRING}" \
            --overwrite
    else
        az storage blob download-batch \
            --source "${CONTAINER}" \
            --destination "${DESTINATION}" \
            --pattern "map.osrm*" \
            --connection-string "${AZURE_STORAGE_CONNECTION_STRING}" \
            --overwrite
    fi
elif [ -n "${AZURE_STORAGE_ACCOUNT_NAME:-}" ]; then
    echo "[+] Downloading via Managed Identity / Azure CLI Login (Account: ${AZURE_STORAGE_ACCOUNT_NAME})..."
    if [ -n "${PREFIX}" ]; then
        az storage blob download-batch \
            --account-name "${AZURE_STORAGE_ACCOUNT_NAME}" \
            --source "${CONTAINER}" \
            --destination "${DESTINATION}" \
            --pattern "${PREFIX}/*" \
            --auth-mode login \
            --overwrite
    else
        az storage blob download-batch \
            --account-name "${AZURE_STORAGE_ACCOUNT_NAME}" \
            --source "${CONTAINER}" \
            --destination "${DESTINATION}" \
            --pattern "map.osrm*" \
            --auth-mode login \
            --overwrite
    fi
else
    echo "[!] ERROR: No valid Azure credentials found. Set AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_CONNECTION_STRING." >&2
    exit 1
fi

# Flatten directory if downloaded under a prefix folder
if [ -n "${PREFIX}" ] && [ -d "${DESTINATION}/${PREFIX}" ]; then
    mv "${DESTINATION}/${PREFIX}"/map.osrm* "${DESTINATION}/" 2>/dev/null || true
    rm -rf "${DESTINATION}/${PREFIX}"
fi

# 3. Verification
echo "======================================================================"
echo ">> Verifying OSRM MLD Dataset Integrity..."
if [ ! -f "${DESTINATION}/map.osrm" ]; then
    echo "[!] ERROR: map.osrm was not found in ${DESTINATION}" >&2
    exit 1
fi

if [ ! -f "${DESTINATION}/map.osrm.partition" ] || [ ! -f "${DESTINATION}/map.osrm.cells" ]; then
    echo "[!] WARNING: Missing map.osrm.partition or map.osrm.cells! MLD algorithm will fail." >&2
else
    echo "[✓] MLD dataset verified successfully:"
    ls -lh "${DESTINATION}"/map.osrm*
fi
echo "======================================================================"
