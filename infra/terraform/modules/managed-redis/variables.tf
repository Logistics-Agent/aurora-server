variable "enable_managed_redis" {
  description = "Conditionally deploy Azure Managed Redis (true for demo/prod, false when destroyed to cease billing)"
  type        = bool
  default     = false
}

variable "enable_private_endpoint" {
  description = "Whether to create Private Endpoint for Managed Redis"
  type        = bool
  default     = true
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
  description = "Enterprise SKU name (e.g. Enterprise_E10-2, Enterprise_E20-2, Enterprise_E5)"
  type        = string
  default     = "Enterprise_E10-2"
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "subnet_id" {
  description = "Subnet ID for Private Endpoint attachment"
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
