# =============================================================================
# Azure Managed Redis (AMR) Module
# =============================================================================

resource "azurerm_redis_enterprise_cluster" "redis" {
  count = var.enable_managed_redis ? 1 : 0

  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku_name            = var.sku_name
  zones               = var.availability_zones

  tags = var.tags
}

resource "azurerm_redis_enterprise_database" "redis_db" {
  count = var.enable_managed_redis ? 1 : 0

  name              = "default"
  cluster_id        = azurerm_redis_enterprise_cluster.redis[0].id
  client_protocol   = "Encrypted"
  clustering_policy = "EnterpriseCluster"
  eviction_policy   = "VolatileLRU"
  port              = 10000
}

# Private Endpoint for Azure Managed Redis
resource "azurerm_private_endpoint" "redis_pe" {
  count = var.enable_managed_redis && var.subnet_id != null ? 1 : 0

  name                = "pe-redis-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.subnet_id

  private_service_connection {
    name                           = "psc-redis-${var.name}"
    private_connection_resource_id = azurerm_redis_enterprise_cluster.redis[0].id
    is_manual_connection           = false
    subresource_names              = ["redisEnterprise"]
  }

  dynamic "private_dns_zone_group" {
    for_each = var.private_dns_zone_id != null ? [1] : []
    content {
      name                 = "pdz-group-redis"
      private_dns_zone_ids = [var.private_dns_zone_id]
    }
  }

  tags = var.tags
}
