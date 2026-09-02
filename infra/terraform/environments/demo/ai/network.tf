# =============================================================================
# Subscription 2: AI / COMPUTE — Networking & Peerings
# =============================================================================

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

module "network" {
  source              = "../../modules/network"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  vnet_name           = var.vnet_name
  address_space       = [var.vnet_cidr]

  subnets = {
    "snet-aks-ai" = {
      address_prefix                            = var.aks_subnet_cidr
      private_endpoint_network_policies_enabled = true
    }
  }

  peerings = {
    "peering-ai-to-shared" = {
      remote_vnet_id = var.shared_vnet_id
    }
    "peering-ai-to-core" = {
      remote_vnet_id = var.core_vnet_id
    }
  }

  tags = local.tags
}
