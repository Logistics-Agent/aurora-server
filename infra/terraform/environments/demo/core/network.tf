# =============================================================================
# Subscription 1: CORE — Networking & Peerings
# =============================================================================

module "resource_group" {
  source   = "../../../modules/resource-group"
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

module "network" {
  source              = "../../../modules/network"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  vnet_name           = var.vnet_name
  address_space       = [var.vnet_cidr]

  subnets = {
    "snet-aks-core" = {
      address_prefix                            = var.aks_subnet_cidr
      private_endpoint_network_policies_enabled = true
    }
    "snet-appgw" = {
      address_prefix                            = var.appgw_subnet_cidr
      private_endpoint_network_policies_enabled = true
    }
  }

  enable_nat_gateway      = var.enable_nat_gateway
  availability_zones      = var.availability_zones
  nat_gateway_subnet_keys = ["snet-aks-core"]

  peerings = merge(
    var.shared_vnet_id != "" && var.shared_vnet_id != null ? {
      "peering-core-to-shared" = {
        remote_vnet_id = var.shared_vnet_id
      }
    } : {},
    var.ai_vnet_id != "" && var.ai_vnet_id != null ? {
      "peering-core-to-ai" = {
        remote_vnet_id = var.ai_vnet_id
      }
    } : {}
  )

  tags = local.tags
}
