# =============================================================================
# Subscription 1: CORE — Compute (ACR & AKS Core Cluster)
# =============================================================================

module "acr" {
  source              = "../../../modules/acr"
  name                = var.acr_name
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku                 = "Standard"
  admin_enabled       = true
  tags                = local.tags
}

module "aks" {
  source              = "../../../modules/aks"
  cluster_name        = var.cluster_name
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  dns_prefix          = var.dns_prefix
  kubernetes_version  = var.kubernetes_version
  subnet_id           = module.network.subnet_ids["snet-aks-core"]
  node_count          = var.node_count
  node_vm_size        = var.node_vm_size
  availability_zones  = var.availability_zones
  acr_id              = module.acr.id
  environment         = var.environment

  workload_identities = {
    "aks-core" = {
      namespace       = "aurora"
      service_account = "sa-aurora-core"
    }
    "mail-backend" = {
      namespace       = "aurora"
      service_account = "sa-mail-backend"
    }
  }

  tags = local.tags
}
