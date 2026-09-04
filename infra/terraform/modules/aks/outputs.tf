output "id" {
  description = "AKS Cluster ID"
  value       = azurerm_kubernetes_cluster.aks.id
}

output "name" {
  description = "AKS Cluster name"
  value       = azurerm_kubernetes_cluster.aks.name
}

output "oidc_issuer_url" {
  description = "OIDC Issuer URL for Workload Identity"
  value       = azurerm_kubernetes_cluster.aks.oidc_issuer_url
}

output "kubelet_identity_object_id" {
  description = "Kubelet Managed Identity Object ID"
  value       = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}

output "workload_identity_client_ids" {
  description = "Map of Workload Identity name to Client ID"
  value       = { for k, v in azurerm_user_assigned_identity.workload_identities : k => v.client_id }
}

output "workload_identity_principal_ids" {
  description = "Map of Workload Identity name to Principal ID"
  value       = { for k, v in azurerm_user_assigned_identity.workload_identities : k => v.principal_id }
}

output "kube_config_raw" {
  description = "Raw kubeconfig for cluster access"
  value       = azurerm_kubernetes_cluster.aks.kube_config_raw
  sensitive   = true
}
