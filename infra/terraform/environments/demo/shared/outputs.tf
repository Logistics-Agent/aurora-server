output "shared_resource_group_name" {
  description = "Shared Resource Group name"
  value       = module.resource_group.name
}

output "shared_vnet_id" {
  description = "Shared VNet ID (Copy to core and ai terraform.tfvars)"
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

output "osrm_container_resource_manager_id" {
  description = "Resource Manager ID of OSRM Blob container (Copy to ai terraform.tfvars as shared_osrm_container_resource_manager_id)"
  value       = lookup(module.storage.container_resource_manager_ids, "osrm", null)
}

output "ocr_docs_container_resource_manager_id" {
  description = "Resource Manager ID of OCR Docs Blob container"
  value       = lookup(module.storage.container_resource_manager_ids, "ocr-docs", null)
}

output "storage_container_resource_manager_ids" {
  description = "Map of application storage container Resource Manager IDs"
  value       = module.storage.container_resource_manager_ids
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = module.key_vault.name
}

output "key_vault_id" {
  description = "Key Vault ID (Copy to core and ai terraform.tfvars as shared_key_vault_id)"
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

# AWS Outputs (Conditional)
output "aws_cognito_user_pool_id" {
  description = "AWS Cognito User Pool ID"
  value       = length(module.aws_cognito) > 0 ? module.aws_cognito[0].user_pool_id : null
}

output "aws_cognito_client_id" {
  description = "AWS Cognito App Client ID"
  value       = length(module.aws_cognito) > 0 ? module.aws_cognito[0].client_id : null
}

output "aws_cognito_client_secret" {
  description = "AWS Cognito App Client Secret"
  value       = length(module.aws_cognito) > 0 ? module.aws_cognito[0].client_secret : null
  sensitive   = true
}

output "aws_iam_access_key_id" {
  description = "AWS IAM Access Key ID for IamTenant"
  value       = length(module.aws_cognito) > 0 ? module.aws_cognito[0].iam_access_key_id : null
}

output "aws_iam_secret_access_key" {
  description = "AWS IAM Secret Access Key for IamTenant"
  value       = length(module.aws_cognito) > 0 ? module.aws_cognito[0].iam_secret_access_key : null
  sensitive   = true
}

# Cloudflare Outputs (Conditional)
output "cloudflare_r2_bucket_name" {
  description = "Cloudflare R2 Bucket name for Mail Service"
  value       = length(module.cloudflare_r2) > 0 ? module.cloudflare_r2[0].bucket_name : null
}

output "cloudflare_r2_endpoint_url" {
  description = "Cloudflare R2 S3-compatible Endpoint URL"
  value       = length(module.cloudflare_r2) > 0 ? module.cloudflare_r2[0].endpoint_url : null
}
