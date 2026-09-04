# =============================================================================
# Aurora Route Optimization — OSRM Map Dataset Downloader (Azure Blob Storage)
# =============================================================================
# Target: Downloads MLD-compiled OSRM map dataset (.osrm*) from Azure Blob Storage
# to the local ./data volume mounted by osrm-routed.
#
# Authentication Precedence:
#   1. Azure Managed Identity / Workload Identity / Azure CLI login (Default for AKS/VMs):
#      - Set AZURE_STORAGE_ACCOUNT_NAME
#      - (Optional) Set AZURE_STORAGE_CONTAINER (default: osrm-data)
#      - (Optional) Set AZURE_OSRM_BLOB_PREFIX (e.g. "osrm/" or empty)
#   2. Connection String (For local development):
#      - Set AZURE_STORAGE_CONNECTION_STRING
#
# Environment Variables:
#   OSRM_STORAGE_PROVIDER         — "AZURE_BLOB" (default)
#   AZURE_STORAGE_ACCOUNT_NAME    — Azure Storage Account name (e.g. staurorarouteplanningdev)
#   AZURE_STORAGE_CONTAINER       — Blob container name (default: osrm-data)
#   AZURE_OSRM_BLOB_PREFIX        — Blob prefix inside container (default: "")
#   AZURE_STORAGE_CONNECTION_STRING — Storage account connection string (optional fallback)
#   FORCE_DOWNLOAD                — Set to "true" to force re-download even if files exist
# =============================================================================

[CmdletBinding()]
param (
    [string]$AccountName = $env:AZURE_STORAGE_ACCOUNT_NAME,
    [string]$Container = $(if ($env:AZURE_STORAGE_CONTAINER) { $env:AZURE_STORAGE_CONTAINER } else { "osrm" }),
    [string]$Prefix = $env:AZURE_OSRM_BLOB_PREFIX,
    [string]$ConnectionString = $env:AZURE_STORAGE_CONNECTION_STRING,
    [switch]$Force = ($env:FORCE_DOWNLOAD -eq "true" -or $env:FORCE_DOWNLOAD -eq "1")
)

$ErrorActionPreference = "Stop"
$destination = Join-Path $PSScriptRoot "data"
New-Item -ItemType Directory -Force $destination | Out-Null

Write-Host "======================================================================"
Write-Host ">> Aurora OSRM Dataset Downloader (Azure Blob Storage)"
Write-Host "======================================================================"
Write-Host "Storage Provider : $($env:OSRM_STORAGE_PROVIDER ?? 'AZURE_BLOB')"
Write-Host "Container        : $Container"
Write-Host "Prefix           : $(if ($Prefix) { $Prefix } else { '<root>' })"
Write-Host "Destination      : $destination"

# 1. Check existing cache/dataset to avoid redundant re-downloads
$primaryFile = Join-Path $destination "map.osrm"
$partitionFile = Join-Path $destination "map.osrm.partition"
$cellsFile = Join-Path $destination "map.osrm.cells"

if (-not $Force -and (Test-Path $primaryFile) -and (Test-Path $partitionFile) -and (Test-Path $cellsFile)) {
    $fileCount = (Get-ChildItem $destination -Filter "map.osrm*").Count
    Write-Host "[✓] OSRM MLD dataset already present in local volume ($fileCount files found)."
    Write-Host "    Found required MLD files: map.osrm, map.osrm.partition, map.osrm.cells."
    Write-Host "    Skipping download. Use -Force or set FORCE_DOWNLOAD=true to override."
    exit 0
}

# 2. Execute Download via Azure CLI or AzCopy
$pattern = if ($Prefix) { "$Prefix/map.osrm*" } else { "map.osrm*" }

if ($ConnectionString) {
    Write-Host "[+] Downloading .osrm* batch using Connection String..."
    if ($Prefix) {
        az storage blob download-batch `
            --source $Container `
            --destination $destination `
            --pattern "$Prefix/*" `
            --connection-string $ConnectionString `
            --overwrite
    } else {
        az storage blob download-batch `
            --source $Container `
            --destination $destination `
            --pattern "map.osrm*" `
            --connection-string $ConnectionString `
            --overwrite
    }
} elseif ($AccountName) {
    Write-Host "[+] Downloading .osrm* batch using Managed Identity / Azure CLI Login (Account: $AccountName)..."
    if ($Prefix) {
        az storage blob download-batch `
            --account-name $AccountName `
            --source $Container `
            --destination $destination `
            --pattern "$Prefix/*" `
            --auth-mode login `
            --overwrite
    } else {
        az storage blob download-batch `
            --account-name $AccountName `
            --source $Container `
            --destination $destination `
            --pattern "map.osrm*" `
            --auth-mode login `
            --overwrite
    }
} else {
    Write-Error "No valid Azure authentication found. Please provide either AZURE_STORAGE_ACCOUNT_NAME (for Managed Identity) or AZURE_STORAGE_CONNECTION_STRING."
}

# If downloaded with a prefix folder, move files to root of $destination
if ($Prefix -and (Test-Path (Join-Path $destination $Prefix))) {
    $prefixPath = Join-Path $destination $Prefix
    Get-ChildItem $prefixPath -Filter "map.osrm*" | Move-Item -Destination $destination -Force
    Remove-Item $prefixPath -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Verify Downloaded Files
Write-Host "======================================================================"
Write-Host ">> Verifying OSRM MLD Dataset Integrity..."
$downloadedFiles = Get-ChildItem $destination -Filter "map.osrm*"

if ($downloadedFiles.Count -eq 0) {
    Write-Error "Download failed: No map.osrm* files found in $destination"
}

$hasPartition = Test-Path $partitionFile
$hasCells = Test-Path $cellsFile

if (-not $hasPartition -or -not $hasCells) {
    Write-Warning "WARNING: Missing MLD files (map.osrm.partition or map.osrm.cells). OSRM MLD algorithm may fail to start!"
} else {
    Write-Host "[✓] MLD dataset verified successfully ($($downloadedFiles.Count) files):"
    $downloadedFiles | Select-Object Name, @{Name="Size (MB)";Expression={[math]::Round($_.Length / 1MB, 2)}} | Format-Table -AutoSize
}
Write-Host "======================================================================"
