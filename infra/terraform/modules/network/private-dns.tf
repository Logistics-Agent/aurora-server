# =============================================================================
# Azure Private DNS Zones & VNet Links
# =============================================================================

resource "azurerm_private_dns_zone" "dns_zones" {
  for_each = var.private_dns_zones

  name                = each.key
  resource_group_name = var.resource_group_name

  tags = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "vnet_links" {
  for_each = {
    for pair in flatten([
      for zone_key, zone in var.private_dns_zones : [
        for link in lookup(zone, "vnet_links", []) : {
          key     = "${zone_key}-${link.name}"
          zone    = zone_key
          name    = link.name
          vnet_id = link.vnet_id
        }
      ]
    ]) : pair.key => pair
  }

  name                  = each.value.name
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.dns_zones[each.value.zone].name
  virtual_network_id    = each.value.vnet_id
  registration_enabled  = false

  tags = var.tags
}
