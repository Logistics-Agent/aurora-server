# =============================================================================
# Managed Identities & Workload Identity Federation
# =============================================================================

# AKS Control Plane User-Assigned Identity
resource "azurerm_user_assigned_identity" "aks_control_plane" {
  name                = "uami-${var.cluster_name}-cp"
  resource_group_name = var.resource_group_name
  location            = var.location

  tags = var.tags
}

# Network Contributor on AKS Subnet
resource "azurerm_role_assignment" "aks_network_contributor" {
  scope                = var.subnet_id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_user_assigned_identity.aks_control_plane.principal_id
}

# Optional AcrPull attachment
resource "azurerm_role_assignment" "aks_acrpull" {
  count = var.acr_id != null ? 1 : 0

  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}

# Scoped Workload Identities
resource "azurerm_user_assigned_identity" "workload_identities" {
  for_each = var.workload_identities

  name                = "uami-${each.key}-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location

  tags = var.tags
}

# Federated Identity Credentials for Kubernetes ServiceAccounts
resource "azurerm_federated_identity_credential" "fed_creds" {
  for_each = var.workload_identities

  name                = "fed-${each.key}-${var.environment}"
  resource_group_name = var.resource_group_name
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.aks.oidc_issuer_url
  parent_id           = azurerm_user_assigned_identity.workload_identities[each.key].id
  subject             = "system:serviceaccount:${each.value.namespace}:${each.value.service_account}"
}
