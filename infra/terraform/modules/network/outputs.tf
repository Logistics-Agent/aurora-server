output "vnet_id" {
  description = "VNet ID"
  value       = azurerm_virtual_network.vnet.id
}

output "vnet_name" {
  description = "VNet name"
  value       = azurerm_virtual_network.vnet.name
}

output "subnet_ids" {
  description = "Map of subnet name to subnet ID"
  value       = { for k, v in azurerm_subnet.subnets : k => v.id }
}

output "private_dns_zone_ids" {
  description = "Map of Private DNS Zone IDs"
  value       = { for k, v in azurerm_private_dns_zone.dns_zones : k => v.id }
}
