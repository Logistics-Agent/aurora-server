variable "name" {
  description = "Storage Account name (alphanumeric lowercase)"
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

variable "replication_type" {
  description = "Storage replication type (LRS, ZRS, GRS)"
  type        = string
  default     = "LRS"
}

variable "enable_private_endpoint" {
  description = "Whether to create Private Endpoint for Storage Account"
  type        = bool
  default     = true
}

variable "subnet_id" {
  description = "Subnet ID for Private Endpoint attachment"
  type        = string
  default     = null
}

variable "private_dns_zone_id" {
  description = "Optional Private DNS Zone ID for privatelink.blob.core.windows.net"
  type        = string
  default     = null
}

variable "containers" {
  description = "Map of container names to config"
  type = map(object({
    access_type = optional(string, "private")
  }))
  default = {}
}

variable "osrm_reader_principal_id" {
  description = "Optional Principal ID of uami-osrm-reader to grant Storage Blob Data Reader"
  type        = string
  default     = null
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}
