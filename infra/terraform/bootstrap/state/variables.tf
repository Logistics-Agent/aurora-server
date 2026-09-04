variable "subscription_id" {
  description = "Azure Subscription 3 (Shared) ID"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name for Terraform remote state"
  type        = string
  default     = "rg-aurora-tfstate-demo"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "southeastasia"
}

variable "storage_account_name" {
  description = "Globally unique name for Storage Account A (3-24 alphanumeric lowercase)"
  type        = string
  default     = "stauroratfstatedemo"
}

variable "container_name" {
  description = "Blob container name for tfstate"
  type        = string
  default     = "tfstate"
}

variable "tags" {
  description = "Standard resource tags"
  type        = map(string)
  default = {
    Project     = "Aurora"
    Environment = "demo"
    ManagedBy   = "Terraform"
  }
}
