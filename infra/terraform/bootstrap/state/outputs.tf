output "terraform_state_resource_group_name" {
  description = "Resource Group containing the tfstate Storage Account"
  value       = azurerm_resource_group.tfstate.name
}

output "terraform_state_storage_account_name" {
  description = "Storage Account A name for remote backend"
  value       = azurerm_storage_account.tfstate.name
}

output "terraform_state_container_name" {
  description = "Container name for remote backend"
  value       = azurerm_storage_container.tfstate.name
}
