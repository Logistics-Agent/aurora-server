# =============================================================================
# Tải OSRM map data (đã build MLD) từ Azure Blob Storage về ./data
# =============================================================================
# Yêu cầu env vars:
#   AZURE_STORAGE_CONNECTION_STRING  — connection string của storage account
#   OSRM_DATA_CONTAINER              — tên container (mặc định: osrm-data)
#
# Dùng Azure CLI (az). Thay bằng azcopy nếu data lớn:
#   azcopy copy "https://<account>.blob.core.windows.net/<container>/*" ./data
# =============================================================================

$ErrorActionPreference = "Stop"

$container = if ($env:OSRM_DATA_CONTAINER) { $env:OSRM_DATA_CONTAINER } else { "osrm-data" }
$destination = Join-Path $PSScriptRoot "data"

if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error "Thiếu env var AZURE_STORAGE_CONNECTION_STRING"
}

New-Item -ItemType Directory -Force $destination | Out-Null

Write-Host "Đang tải OSRM data (MLD) từ container '$container' về $destination ..."

az storage blob download-batch `
    --source $container `
    --destination $destination `
    --connection-string $env:AZURE_STORAGE_CONNECTION_STRING `
    --overwrite

Write-Host "Hoàn tất. Kiểm tra file map.osrm.partition / map.osrm.cells (bắt buộc cho MLD):"
Get-ChildItem $destination -Filter "map.osrm*" | Select-Object Name, Length
