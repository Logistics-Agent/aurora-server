# =============================================================================
# Aurora Route Optimization — Terraform Outputs for OSRM Azure Blob Storage
# =============================================================================
output "storage_account_name" {
  description = "Name of the created Azure Storage Account"
  value       = azurerm_storage_account.osrm_storage.name
}

output "osrm_container_name" {
  description = "Name of the OSRM dataset container"
  value       = azurerm_storage_container.osrm_container.name
}

output "primary_blob_endpoint" {
  description = "Primary Blob Service Endpoint URL"
  value       = azurerm_storage_account.osrm_storage.primary_blob_endpoint
}

output "osrm_managed_identity_client_id" {
  description = "Client ID of the User Assigned Identity for AKS/VM Workload Identity"
  value       = azurerm_user_assigned_identity.osrm_identity.client_id
}

output "osrm_managed_identity_principal_id" {
  description = "Principal ID of the User Assigned Identity"
  value       = azurerm_user_assigned_identity.osrm_identity.principal_id
}
