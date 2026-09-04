output "id" {
  description = "ACR ID"
  value       = azurerm_container_registry.acr.id
}

output "name" {
  description = "ACR Name"
  value       = azurerm_container_registry.acr.name
}

output "login_server" {
  description = "ACR login server URL"
  value       = azurerm_container_registry.acr.login_server
}

output "admin_username" {
  description = "ACR admin username"
  value       = azurerm_container_registry.acr.admin_username
}

output "admin_password" {
  description = "ACR admin password"
  value       = azurerm_container_registry.acr.admin_password
  sensitive   = true
}
