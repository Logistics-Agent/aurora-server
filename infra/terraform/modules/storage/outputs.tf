output "id" {
  description = "Storage Account ID"
  value       = azurerm_storage_account.storage.id
}

output "name" {
  description = "Storage Account name"
  value       = azurerm_storage_account.storage.name
}

output "primary_blob_endpoint" {
  description = "Primary Blob Endpoint URL"
  value       = azurerm_storage_account.storage.primary_blob_endpoint
}

output "container_names" {
  description = "Map of created container names"
  value       = { for k, v in azurerm_storage_container.containers : k => v.name }
}

output "container_resource_manager_ids" {
  description = "Map of container Resource Manager IDs"
  value       = { for k, v in azurerm_storage_container.containers : k => v.resource_manager_id }
}
