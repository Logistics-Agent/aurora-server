Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Azure Infrastructure Status Check       " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

Write-Host "`n[Core Cluster - Sub MHuy]" -ForegroundColor Yellow
az aks show --resource-group rg-aurora-core-demo --name aks-core-demo --subscription dc06bbd6-8679-4ea1-88d5-069d9ba9916d --query "{Name:name, Provisioning:provisioningState, PowerState:powerState.code}" --output table

Write-Host "`n[AI Cluster - Sub Thuan]" -ForegroundColor Yellow
az aks show --resource-group rg-aurora-ai-demo --name aks-ai-demo --subscription caf4fd36-3398-46cc-81a1-6f8db18a179b --query "{Name:name, Provisioning:provisioningState, PowerState:powerState.code}" --output table

Write-Host "`n[Shared Hub - Sub Tan]" -ForegroundColor Yellow
Write-Host "Status: Passive storage & Key Vault (No running compute resources, $0 compute cost)" -ForegroundColor Green
