# =============================================================================
# NAT Gateway (Conditionally created for future production; disabled in demo)
# =============================================================================

resource "azurerm_public_ip" "nat_pip" {
  count = var.enable_nat_gateway ? 1 : 0

  name                = "pip-nat-${var.vnet_name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  allocation_method   = "Static"
  sku                 = "Standard"
  zones               = var.availability_zones

  tags = var.tags
}

resource "azurerm_nat_gateway" "nat_gw" {
  count = var.enable_nat_gateway ? 1 : 0

  name                    = "natgw-${var.vnet_name}"
  location                = var.location
  resource_group_name     = var.resource_group_name
  sku_name                = "Standard"
  idle_timeout_in_minutes = 10
  zones                   = var.availability_zones

  tags = var.tags
}

resource "azurerm_nat_gateway_public_ip_association" "nat_pip_assoc" {
  count = var.enable_nat_gateway ? 1 : 0

  nat_gateway_id       = azurerm_nat_gateway.nat_gw[0].id
  public_ip_address_id = azurerm_public_ip.nat_pip[0].id
}

resource "azurerm_subnet_nat_gateway_association" "nat_subnet_assoc" {
  for_each = var.enable_nat_gateway ? var.nat_gateway_subnet_keys : toset([])

  subnet_id      = azurerm_subnet.subnets[each.key].id
  nat_gateway_id = azurerm_nat_gateway.nat_gw[0].id
}
