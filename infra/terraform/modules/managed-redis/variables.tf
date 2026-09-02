variable "enable_managed_redis" {
  description = "Conditionally deploy Azure Managed Redis (true for demo/prod, false when destroyed to cease billing)"
  type        = bool
  default     = false
}

variable "name" {
  description = "Azure Managed Redis cluster name"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
}

variable "sku_name" {
  description = "Enterprise SKU name (e.g. Balanced_B0, Balanced_B1, MemoryOptimized_M10)"
  type        = string
  default     = "Balanced_B0"
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "subnet_id" {
  description = "Optional subnet ID for Private Endpoint attachment"
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Optional Private DNS Zone ID for privatelink.redis.azure.net"
  type        = string
  default     = null
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}
