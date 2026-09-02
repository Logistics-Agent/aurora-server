# =============================================================================
# Aurora Route Optimization — Terraform Variables for OSRM Azure Blob Storage
# =============================================================================
variable "environment" {
  type        = string
  description = "Target deployment environment (dev, staging, prod)"
  default     = "dev"
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  type        = string
  description = "Azure Region for storage resources"
  default     = "southeastasia"
}

variable "create_resource_group" {
  type        = bool
  description = "Whether to create a new resource group or reference existing"
  default     = false
}

variable "resource_group_name" {
  type        = string
  description = "Azure Resource Group name"
  default     = "rg-aurora-routeplanning-dev"
}

variable "storage_account_name" {
  type        = string
  description = "Globally unique name of the Azure Storage Account (3-24 lowercase alphanumeric)"
}

variable "container_name" {
  type        = string
  description = "Name of the Blob container for OSRM datasets"
  default     = "osrm-data"
}
