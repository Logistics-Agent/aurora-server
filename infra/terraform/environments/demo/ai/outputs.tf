output "ai_resource_group_name" {
  description = "AI Resource Group name"
  value       = module.resource_group.name
}

output "ai_vnet_id" {
  description = "AI VNet ID"
  value       = module.network.vnet_id
}

output "aks_subnet_id" {
  description = "AKS AI Subnet ID"
  value       = module.network.subnet_ids["snet-aks-ai"]
}

output "aks_cluster_name" {
  description = "AKS AI Cluster name"
  value       = module.aks.name
}

output "aks_cluster_id" {
  description = "AKS AI Cluster ID"
  value       = module.aks.id
}

output "aks_oidc_issuer_url" {
  description = "OIDC Issuer URL for AI Workload Identity"
  value       = module.aks.oidc_issuer_url
}

output "osrm_reader_client_id" {
  description = "Client ID for OSRM Blob Reader Workload Identity"
  value       = module.aks.workload_identity_client_ids["osrm-reader"]
}
