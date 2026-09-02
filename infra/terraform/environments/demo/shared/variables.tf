variable "subscription_id" {
  description = "Azure Subscription 3 (Shared) ID"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name for shared infrastructure"
  type        = string
  default     = "rg-aurora-shared-demo"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "southeastasia"
}

variable "vnet_name" {
  description = "Virtual network name"
  type        = string
  default     = "vnet-aurora-shared"
}

variable "vnet_cidr" {
  description = "CIDR block for shared VNet (10.30.0.0/16)"
  type        = string
  default     = "10.30.0.0/16"
}

variable "private_endpoints_subnet_cidr" {
  description = "CIDR block for private endpoints subnet"
  type        = string
  default     = "10.30.1.0/24"
}

variable "application_storage_account_name" {
  description = "Storage Account B name for application data (OSRM maps & OCR blobs)"
  type        = string
  default     = "stauroradatademo"
}

variable "key_vault_name" {
  description = "Key Vault name"
  type        = string
  default     = "kv-aurora-shared-demo"
}

variable "enable_managed_redis" {
  description = "Set to true to provision Azure Managed Redis for demo; false to cease billing"
  type        = bool
  default     = false
}

variable "redis_name" {
  description = "Azure Managed Redis cluster name"
  type        = string
  default     = "redis-aurora-shared-demo"
}

variable "redis_sku_name" {
  description = "Azure Managed Redis Enterprise SKU"
  type        = string
  default     = "Balanced_B0"
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "demo"
}
