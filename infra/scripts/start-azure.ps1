Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Starting All Azure Clusters (Core + AI) " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Start Core Cluster (Sub MHuy)
Write-Host "[1/2] Starting aks-core-demo (Sub: MHuy)..." -ForegroundColor Yellow
az aks start --resource-group rg-aurora-core-demo --name aks-core-demo --subscription dc06bbd6-8679-4ea1-88d5-069d9ba9916d --no-wait

# 2. Start AI Cluster (Sub Thuan)
Write-Host "[2/2] Starting aks-ai-demo (Sub: Thuan)..." -ForegroundColor Yellow
az aks start --resource-group rg-aurora-ai-demo --name aks-ai-demo --subscription caf4fd36-3398-46cc-81a1-6f8db18a179b --no-wait

Write-Host "`nAll start commands dispatched! Clusters will be ready in 2-3 minutes." -ForegroundColor Green
Write-Host "Run .\infra\scripts\status-azure.ps1 to verify cluster status." -ForegroundColor Gray
