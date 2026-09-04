output "id" {
  description = "Azure Managed Redis Cluster ID"
  value       = var.enable_managed_redis ? azurerm_redis_enterprise_cluster.redis[0].id : null
}

output "hostname" {
  description = "Azure Managed Redis private hostname (<name>.<region>.redis.azure.net)"
  value       = var.enable_managed_redis ? azurerm_redis_enterprise_cluster.redis[0].hostname : null
}

output "port" {
  description = "Azure Managed Redis port"
  value       = var.enable_managed_redis ? azurerm_redis_enterprise_database.redis_db[0].port : 10000
}

output "primary_access_key" {
  description = "Primary access key for Redis authentication"
  value       = var.enable_managed_redis ? azurerm_redis_enterprise_database.redis_db[0].primary_access_key : null
  sensitive   = true
}
