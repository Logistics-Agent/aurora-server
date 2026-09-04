# =============================================================================
# Network Module — VNet, Subnets & NSGs
# =============================================================================

resource "azurerm_virtual_network" "vnet" {
  name                = var.vnet_name
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = var.address_space

  tags = var.tags
}

resource "azurerm_subnet" "subnets" {
  for_each = var.subnets

  name                 = each.key
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = [each.value.address_prefix]

  private_endpoint_network_policies = lookup(each.value, "private_endpoint_network_policies", lookup(each.value, "private_endpoint_network_policies_enabled", true) ? "Enabled" : "Disabled")
}

resource "azurerm_network_security_group" "nsg" {
  for_each = var.subnets

  name                = "nsg-${each.key}"
  location            = var.location
  resource_group_name = var.resource_group_name

  tags = var.tags
}

# Required NSG rules for Application Gateway v2 subnet (GatewayManager ports 65200-65535 & HTTP/S)
resource "azurerm_network_security_rule" "appgw_gateway_manager" {
  count                       = contains(keys(var.subnets), "snet-appgw") ? 1 : 0
  name                        = "AllowGatewayManager"
  priority                    = 100
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "65200-65535"
  source_address_prefix       = "GatewayManager"
  destination_address_prefix  = "*"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.nsg["snet-appgw"].name

  depends_on = [azurerm_network_security_group.nsg]
}

resource "azurerm_network_security_rule" "appgw_http" {
  count                       = contains(keys(var.subnets), "snet-appgw") ? 1 : 0
  name                        = "AllowHTTPInbound"
  priority                    = 110
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "80"
  source_address_prefix       = "*"
  destination_address_prefix  = "*"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.nsg["snet-appgw"].name

  depends_on = [azurerm_network_security_group.nsg]
}

resource "azurerm_network_security_rule" "appgw_https" {
  count                       = contains(keys(var.subnets), "snet-appgw") ? 1 : 0
  name                        = "AllowHTTPSInbound"
  priority                    = 120
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  destination_port_range      = "443"
  source_address_prefix       = "*"
  destination_address_prefix  = "*"
  resource_group_name         = var.resource_group_name
  network_security_group_name = azurerm_network_security_group.nsg["snet-appgw"].name

  depends_on = [azurerm_network_security_group.nsg]
}

resource "azurerm_subnet_network_security_group_association" "nsg_assoc" {
  for_each = var.subnets

  subnet_id                 = azurerm_subnet.subnets[each.key].id
  network_security_group_id = azurerm_network_security_group.nsg[each.key].id
}
