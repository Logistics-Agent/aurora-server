# =============================================================================
# Subscription 3: Shared Infrastructure — Data Platform (Storage & Redis)
# =============================================================================

# Storage Account B: Application Data (OSRM Maps & OCR Blobs)
module "storage" {
  source              = "../../modules/storage"
  name                = var.application_storage_account_name
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  replication_type    = "LRS"

  subnet_id           = module.network.subnet_ids["snet-private-endpoints"]
  private_dns_zone_id = module.network.private_dns_zone_ids["privatelink.blob.core.windows.net"]

  containers = {
    "osrm" = {
      access_type = "private"
    }
    "ocr-docs" = {
      access_type = "private"
    }
  }

  tags = local.tags
}

# Azure Managed Redis (AMR)
module "managed_redis" {
  source               = "../../modules/managed-redis"
  enable_managed_redis = var.enable_managed_redis
  name                 = var.redis_name
  resource_group_name  = module.resource_group.name
  location             = module.resource_group.location
  sku_name             = var.redis_sku_name
  availability_zones   = var.availability_zones

  subnet_id           = module.network.subnet_ids["snet-private-endpoints"]
  private_dns_zone_id = module.network.private_dns_zone_ids["privatelink.redis.azure.net"]

  tags = local.tags
}
