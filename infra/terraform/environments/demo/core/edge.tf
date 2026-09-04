# =============================================================================
# Subscription 1: CORE — Edge (Application Gateway v2 WAF)
# =============================================================================

module "application_gateway" {
  source              = "../../../modules/application-gateway"
  name                = var.appgw_name
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  subnet_id           = module.network.subnet_ids["snet-appgw"]
  availability_zones  = var.availability_zones
  tags                = local.tags
}
