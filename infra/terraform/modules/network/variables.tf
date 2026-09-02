variable "resource_group_name" {
  description = "Resource group name"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
}

variable "vnet_name" {
  description = "Virtual Network name"
  type        = string
}

variable "address_space" {
  description = "Address space for the VNet"
  type        = list(string)
}

variable "subnets" {
  description = "Map of subnets to create"
  type = map(object({
    address_prefix                            = string
    private_endpoint_network_policies_enabled = optional(bool, true)
  }))
  default = {}
}

variable "enable_nat_gateway" {
  description = "Conditionally deploy NAT Gateway (false in demo)"
  type        = bool
  default     = false
}

variable "availability_zones" {
  description = "Availability zones for NAT Gateway / Public IPs"
  type        = list(string)
  default     = ["1"]
}

variable "nat_gateway_subnet_keys" {
  description = "List of subnet keys to associate with NAT Gateway"
  type        = set(string)
  default     = []
}

variable "peerings" {
  description = "Map of VNet peerings to establish"
  type = map(object({
    remote_vnet_id               = string
    allow_virtual_network_access = optional(bool, true)
    allow_forwarded_traffic      = optional(bool, true)
    allow_gateway_transit        = optional(bool, false)
    use_remote_gateways          = optional(bool, false)
  }))
  default = {}
}

variable "private_dns_zones" {
  description = "Map of Private DNS Zones with VNet links"
  type = map(object({
    vnet_links = optional(list(object({
      name    = string
      vnet_id = string
    })), [])
  }))
  default = {}
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}
