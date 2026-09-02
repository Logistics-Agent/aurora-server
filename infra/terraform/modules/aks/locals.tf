locals {
  kubeconfig = azurerm_kubernetes_cluster.aks.kube_config_raw
}
