# =============================================================================
# Subscription 2: AI / COMPUTE — Data Platform (OSRM Blob RBAC)
# =============================================================================

# Grant Storage Blob Data Reader to OSRM Downloader Workload Identity
resource "azurerm_role_assignment" "osrm_blob_reader" {
  count = var.shared_osrm_container_resource_manager_id != null ? 1 : 0

  scope                = var.shared_osrm_container_resource_manager_id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = module.aks.workload_identity_principal_ids["osrm-reader"]
}
