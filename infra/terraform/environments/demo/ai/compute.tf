# =============================================================================
# Subscription 2: AI / COMPUTE — Compute (AKS AI Cluster)
# =============================================================================

module "aks" {
  source              = "../../../modules/aks"
  cluster_name        = var.cluster_name
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  dns_prefix          = var.dns_prefix
  kubernetes_version  = var.kubernetes_version
  subnet_id           = module.network.subnet_ids["snet-aks-ai"]
  node_count          = var.node_count
  node_vm_size        = var.node_vm_size
  availability_zones  = var.availability_zones
  acr_id              = var.core_acr_id
  environment         = var.environment

  workload_identities = {
    "aks-ai" = {
      namespace       = "aurora-ai"
      service_account = "sa-aurora-ai"
    }
    "osrm-reader" = {
      namespace       = "aurora-ai"
      service_account = "sa-osrm-downloader"
    }
  }

  tags = local.tags
}

