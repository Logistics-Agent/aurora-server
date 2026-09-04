variable "subscription_id" {
  description = "Azure Subscription 1 (CORE) ID"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name for Core environment"
  type        = string
  default     = "rg-aurora-core-demo"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "southeastasia"
}

variable "vnet_name" {
  description = "Core VNet name"
  type        = string
  default     = "vnet-aurora-core"
}

variable "vnet_cidr" {
  description = "CIDR block for Core VNet (10.10.0.0/16)"
  type        = string
  default     = "10.10.0.0/16"
}

variable "aks_subnet_cidr" {
  description = "CIDR block for AKS Core subnet (10.10.0.0/20)"
  type        = string
  default     = "10.10.0.0/20"
}

variable "appgw_subnet_cidr" {
  description = "CIDR block for AppGW subnet (10.10.16.0/24)"
  type        = string
  default     = "10.10.16.0/24"
}

variable "enable_nat_gateway" {
  description = "Conditionally deploy NAT Gateway (false for demo)"
  type        = bool
  default     = false
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "acr_name" {
  description = "ACR globally unique name"
  type        = string
  default     = "acrauroracoredemo"
}

variable "cluster_name" {
  description = "AKS Core Cluster name"
  type        = string
  default     = "aks-core-demo"
}

variable "dns_prefix" {
  description = "DNS prefix for AKS Core"
  type        = string
  default     = "aurora-core"
}

variable "kubernetes_version" {
  description = "Kubernetes version"
  type        = string
  default     = "1.30.3"
}

variable "node_count" {
  description = "Node count for system node pool"
  type        = number
  default     = 2
}

variable "node_vm_size" {
  description = "VM size for Core cluster nodes"
  type        = string
  default     = "Standard_D2s_v5"
}

variable "appgw_name" {
  description = "Application Gateway name"
  type        = string
  default     = "appgw-aurora-core-demo"
}

variable "shared_vnet_id" {
  description = "VNet ID of Subscription 3 (Shared) for peering"
  type        = string
  default     = ""
}

variable "ai_vnet_id" {
  description = "VNet ID of Subscription 2 (AI) for peering"
  type        = string
  default     = ""
}

variable "shared_key_vault_id" {
  description = "Key Vault ID in Subscription 3 for workload secret RBAC"
  type        = string
  default     = null
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "demo"
}
