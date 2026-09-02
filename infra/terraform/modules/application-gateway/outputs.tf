output "id" {
  description = "Application Gateway ID"
  value       = azurerm_application_gateway.appgw.id
}

output "name" {
  description = "Application Gateway name"
  value       = azurerm_application_gateway.appgw.name
}

output "public_ip_address" {
  description = "Application Gateway Public IP address"
  value       = azurerm_public_ip.appgw_pip.ip_address
}

output "public_ip_id" {
  description = "Application Gateway Public IP resource ID"
  value       = azurerm_public_ip.appgw_pip.id
}
