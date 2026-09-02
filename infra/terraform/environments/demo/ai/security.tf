# =============================================================================
# Subscription 2: AI / COMPUTE — Security & Key Vault Role Assignments
# =============================================================================

# Grant Key Vault Secrets User on Shared Key Vault to AI Workload Identity
resource "azurerm_role_assignment" "ai_kv_secrets_user" {
  count = var.shared_key_vault_id != null ? 1 : 0

  scope                = var.shared_key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.aks.workload_identity_principal_ids["aks-ai"]
}
