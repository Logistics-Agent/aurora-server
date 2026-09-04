output "id" {
  description = "Key Vault ID"
  value       = azurerm_key_vault.kv.id
}

output "name" {
  description = "Key Vault name"
  value       = azurerm_key_vault.kv.name
}

output "vault_uri" {
  description = "Key Vault URI"
  value       = azurerm_key_vault.kv.vault_uri
}
