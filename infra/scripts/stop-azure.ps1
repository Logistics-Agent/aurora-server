Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Stopping All Azure Clusters (Core + AI) " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Stop Core Cluster (Sub MHuy)
Write-Host "[1/2] Stopping aks-core-demo (Sub: MHuy)..." -ForegroundColor Yellow
az aks stop --resource-group rg-aurora-core-demo --name aks-core-demo --subscription dc06bbd6-8679-4ea1-88d5-069d9ba9916d --no-wait

# 2. Stop AI Cluster (Sub Thuan)
Write-Host "[2/2] Stopping aks-ai-demo (Sub: Thuan)..." -ForegroundColor Yellow
az aks stop --resource-group rg-aurora-ai-demo --name aks-ai-demo --subscription caf4fd36-3398-46cc-81a1-6f8db18a179b --no-wait

Write-Host "`nAll stop commands dispatched! Compute nodes are deallocating to save budget." -ForegroundColor Green
Write-Host "Note: Storage Accounts and Key Vault in Sub Tan do not incur compute charges." -ForegroundColor Gray
