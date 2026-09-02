# =============================================================================
# Azure Key Vault Module
# =============================================================================

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "kv" {
  name                       = var.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = var.sku_name
  enable_rbac_authorization = true

  soft_delete_retention_days = 7
  purge_protection_enabled   = false

  network_acls {
    default_action = var.subnet_id != null ? "Deny" : "Allow"
    bypass         = "AzureServices"
  }

  tags = var.tags
}

# Role assignment for current Terraform deployer as Key Vault Administrator
resource "azurerm_role_assignment" "deployer_admin" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Scoped Workload Identity Secrets User assignments
resource "azurerm_role_assignment" "workload_secrets_users" {
  for_each = var.secrets_user_principal_ids

  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value
}

# Private Endpoint for Key Vault
resource "azurerm_private_endpoint" "kv_pe" {
  count = var.subnet_id != null ? 1 : 0

  name                = "pe-kv-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.subnet_id

  private_service_connection {
    name                           = "psc-kv-${var.name}"
    private_connection_resource_id = azurerm_key_vault.kv.id
    is_manual_connection           = false
    subresource_names              = ["vault"]
  }

  dynamic "private_dns_zone_group" {
    for_each = var.private_dns_zone_id != null ? [1] : []
    content {
      name                 = "pdz-group-kv"
      private_dns_zone_ids = [var.private_dns_zone_id]
    }
  }

  tags = var.tags
}
