variable "subscription_id" {
  description = "Azure Subscription 2 (AI / COMPUTE) ID"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name for AI environment"
  type        = string
  default     = "rg-aurora-ai-demo"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "southeastasia"
}

variable "vnet_name" {
  description = "AI VNet name"
  type        = string
  default     = "vnet-aurora-ai"
}

variable "vnet_cidr" {
  description = "CIDR block for AI VNet (10.20.0.0/16)"
  type        = string
  default     = "10.20.0.0/16"
}

variable "aks_subnet_cidr" {
  description = "CIDR block for AKS AI subnet (10.20.0.0/20)"
  type        = string
  default     = "10.20.0.0/20"
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "cluster_name" {
  description = "AKS AI Cluster name"
  type        = string
  default     = "aks-ai-demo"
}

variable "dns_prefix" {
  description = "DNS prefix for AKS AI"
  type        = string
  default     = "aurora-ai"
}

variable "kubernetes_version" {
  description = "Kubernetes version"
  type        = string
  default     = "1.30.3"
}

variable "node_count" {
  description = "Node count for AI system node pool"
  type        = number
  default     = 2
}

variable "node_vm_size" {
  description = "VM size for AI cluster nodes"
  type        = string
  default     = "Standard_D4s_v5"
}

variable "core_acr_id" {
  description = "Optional ACR ID from Core subscription for AcrPull"
  type        = string
  default     = null
}

variable "shared_vnet_id" {
  description = "VNet ID of Subscription 3 (Shared) for peering"
  type        = string
  default     = ""
}

variable "core_vnet_id" {
  description = "VNet ID of Subscription 1 (Core) for peering"
  type        = string
  default     = ""
}

variable "shared_osrm_container_resource_manager_id" {
  description = "Resource Manager ID of osrm container in Subscription 3 for Storage Blob Data Reader RBAC"
  type        = string
  default     = null
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
