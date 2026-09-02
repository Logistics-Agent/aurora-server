# =============================================================================
# Subscription 3: Shared Infrastructure — Security & Key Vault
# =============================================================================

module "key_vault" {
  source              = "../../modules/key-vault"
  name                = var.key_vault_name
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "standard"

  subnet_id           = module.network.subnet_ids["snet-private-endpoints"]
  private_dns_zone_id = module.network.private_dns_zone_ids["privatelink.vaultcore.azure.net"]

  tags = local.tags
}
