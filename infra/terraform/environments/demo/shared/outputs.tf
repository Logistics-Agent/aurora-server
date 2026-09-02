output "shared_resource_group_name" {
  description = "Shared Resource Group name"
  value       = module.resource_group.name
}

output "shared_vnet_id" {
  description = "Shared VNet ID"
  value       = module.network.vnet_id
}

output "shared_vnet_name" {
  description = "Shared VNet name"
  value       = module.network.vnet_name
}

output "private_endpoints_subnet_id" {
  description = "Private Endpoints Subnet ID"
  value       = module.network.subnet_ids["snet-private-endpoints"]
}

output "application_storage_account_name" {
  description = "Storage Account B name for application data"
  value       = module.storage.name
}

output "application_storage_account_id" {
  description = "Storage Account B ID"
  value       = module.storage.id
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = module.key_vault.name
}

output "key_vault_id" {
  description = "Key Vault ID"
  value       = module.key_vault.id
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = module.key_vault.vault_uri
}

output "redis_hostname" {
  description = "Azure Managed Redis private hostname (<name>.<region>.redis.azure.net)"
  value       = module.managed_redis.hostname
}

output "redis_port" {
  description = "Azure Managed Redis port"
  value       = module.managed_redis.port
}

output "private_dns_zone_ids" {
  description = "Map of Private DNS Zone IDs"
  value       = module.network.private_dns_zone_ids
}
