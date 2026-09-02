variable "name" {
  description = "Key Vault globally unique name"
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
  description = "Key Vault SKU (standard, premium)"
  type        = string
  default     = "standard"
}

variable "subnet_id" {
  description = "Optional subnet ID for Private Endpoint attachment"
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Optional Private DNS Zone ID for privatelink.vaultcore.azure.net"
  type        = string
  default     = null
}

variable "secrets_user_principal_ids" {
  description = "Map of Workload Identity keys to Principal IDs granted Key Vault Secrets User"
  type        = map(string)
  default     = {}
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}
