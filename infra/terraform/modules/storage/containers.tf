# =============================================================================
# Blob Containers
# =============================================================================

resource "azurerm_storage_container" "containers" {
  for_each = var.containers

  name                  = each.key
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = lookup(each.value, "access_type", "private")
}

# Role Assignment for OSRM Blob Reader (Least Privilege)
resource "azurerm_role_assignment" "osrm_blob_reader" {
  count = var.osrm_reader_principal_id != null && contains(keys(var.containers), "osrm") ? 1 : 0

  scope                = azurerm_storage_container.containers["osrm"].resource_manager_id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = var.osrm_reader_principal_id
}
