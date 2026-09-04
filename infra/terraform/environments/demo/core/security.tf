# =============================================================================
# Subscription 1: CORE — Security & Key Vault Role Assignments
# =============================================================================

# Grant Key Vault Secrets User on Shared Key Vault to Core Workload Identities
resource "azurerm_role_assignment" "core_kv_secrets_user" {
  count = var.shared_key_vault_id != null && var.shared_key_vault_id != "" ? 1 : 0

  scope                = var.shared_key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.aks.workload_identity_principal_ids["aks-core"]
}

resource "azurerm_role_assignment" "mail_kv_secrets_user" {
  count = var.shared_key_vault_id != null && var.shared_key_vault_id != "" ? 1 : 0

  scope                = var.shared_key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.aks.workload_identity_principal_ids["mail-backend"]
}
