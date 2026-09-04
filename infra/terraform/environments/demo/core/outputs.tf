output "core_resource_group_name" {
  description = "Core Resource Group name"
  value       = module.resource_group.name
}

output "core_vnet_id" {
  description = "Core VNet ID"
  value       = module.network.vnet_id
}

output "core_vnet_name" {
  description = "Core VNet name"
  value       = module.network.vnet_name
}

output "aks_subnet_id" {
  description = "AKS Core Subnet ID"
  value       = module.network.subnet_ids["snet-aks-core"]
}

output "appgw_subnet_id" {
  description = "AppGW Subnet ID"
  value       = module.network.subnet_ids["snet-appgw"]
}

output "aks_cluster_name" {
  description = "AKS Core Cluster name"
  value       = module.aks.name
}

output "aks_cluster_id" {
  description = "AKS Core Cluster ID"
  value       = module.aks.id
}

output "aks_oidc_issuer_url" {
  description = "OIDC Issuer URL for Core Workload Identity"
  value       = module.aks.oidc_issuer_url
}

output "acr_id" {
  description = "Core ACR ID (Needed for AI subscription AcrPull)"
  value       = module.acr.id
}

output "acr_name" {
  description = "Core ACR name"
  value       = module.acr.name
}

output "acr_login_server" {
  description = "ACR login server URL"
  value       = module.acr.login_server
}

output "appgw_public_ip" {
  description = "Public IP address of Application Gateway"
  value       = module.application_gateway.public_ip_address
}
