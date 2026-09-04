# =============================================================================
# Subscription 3: Shared Infrastructure — Networking & Private DNS
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
    "snet-private-endpoints" = {
      address_prefix                            = var.private_endpoints_subnet_cidr
      private_endpoint_network_policies_enabled = true
    }
  }

  peerings = merge(
    var.core_vnet_id != "" && var.core_vnet_id != null ? {
      "peering-shared-to-core" = {
        remote_vnet_id = var.core_vnet_id
      }
    } : {},
    var.ai_vnet_id != "" && var.ai_vnet_id != null ? {
      "peering-shared-to-ai" = {
        remote_vnet_id = var.ai_vnet_id
      }
    } : {}
  )

  private_dns_zones = {
    "privatelink.redis.azure.net" = {
      vnet_links = concat(
        [{ name = "link-shared-vnet", vnet_id = module.network.vnet_id }],
        var.core_vnet_id != "" && var.core_vnet_id != null ? [{ name = "link-core-vnet", vnet_id = var.core_vnet_id }] : [],
        var.ai_vnet_id != "" && var.ai_vnet_id != null ? [{ name = "link-ai-vnet", vnet_id = var.ai_vnet_id }] : []
      )
    }
    "privatelink.vaultcore.azure.net" = {
      vnet_links = concat(
        [{ name = "link-shared-vnet", vnet_id = module.network.vnet_id }],
        var.core_vnet_id != "" && var.core_vnet_id != null ? [{ name = "link-core-vnet", vnet_id = var.core_vnet_id }] : [],
        var.ai_vnet_id != "" && var.ai_vnet_id != null ? [{ name = "link-ai-vnet", vnet_id = var.ai_vnet_id }] : []
      )
    }
    "privatelink.blob.core.windows.net" = {
      vnet_links = concat(
        [{ name = "link-shared-vnet", vnet_id = module.network.vnet_id }],
        var.core_vnet_id != "" && var.core_vnet_id != null ? [{ name = "link-core-vnet", vnet_id = var.core_vnet_id }] : [],
        var.ai_vnet_id != "" && var.ai_vnet_id != null ? [{ name = "link-ai-vnet", vnet_id = var.ai_vnet_id }] : []
      )
    }
  }

  tags = local.tags
}
