variable "cluster_name" {
  description = "AKS Cluster name"
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

variable "dns_prefix" {
  description = "DNS prefix for the cluster"
  type        = string
}

variable "kubernetes_version" {
  description = "Kubernetes version"
  type        = string
  default     = "1.30.3"
}

variable "subnet_id" {
  description = "Subnet ID for the AKS default node pool"
  type        = string
}

variable "node_count" {
  description = "Node count for system node pool"
  type        = number
  default     = 1
}

variable "node_vm_size" {
  description = "VM size for nodes"
  type        = string
  default     = "Standard_D2s_v5"
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

variable "acr_id" {
  description = "Optional ACR ID for AcrPull attachment"
  type        = string
  default     = null
}

variable "environment" {
  description = "Environment name (demo, prod)"
  type        = string
  default     = "demo"
}

variable "workload_identities" {
  description = "Map of workload identity keys to namespace and service_account"
  type = map(object({
    namespace       = string
    service_account = string
  }))
  default = {}
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}
