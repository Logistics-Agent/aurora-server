variable "name" {
  description = "Application Gateway name"
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

variable "subnet_id" {
  description = "Dedicated Application Gateway subnet ID"
  type        = string
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}
