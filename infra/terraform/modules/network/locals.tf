locals {
  subnet_ids = { for k, v in azurerm_subnet.subnets : k => v.id }
}
