# =============================================================================
# Subscription 3: Shared Infrastructure — Variables
# =============================================================================

# 1. Subscription & Resource Group
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

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "demo"
}

# 2. Networking
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

variable "core_vnet_id" {
  description = "Optional Core VNet ID for peering and DNS linking"
  type        = string
  default     = ""
}

variable "ai_vnet_id" {
  description = "Optional AI VNet ID for peering and DNS linking"
  type        = string
  default     = ""
}

variable "availability_zones" {
  description = "Availability zones list (single zone in demo)"
  type        = list(string)
  default     = ["1"]
}

# 3. Data Platform (Storage & Redis)
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

# 4. External Services — AWS Cognito & IAM
variable "enable_aws_cognito" {
  description = "Whether to provision AWS Cognito User Pool, Client, and IAM User for IamTenant"
  type        = bool
  default     = false
}

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "ap-southeast-1"
}

variable "aws_access_key" {
  description = "AWS Access Key for provisioning (optional if AWS_ACCESS_KEY_ID env var is set)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "aws_secret_key" {
  description = "AWS Secret Key for provisioning (optional if AWS_SECRET_ACCESS_KEY env var is set)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "aws_cognito_user_pool_name" {
  description = "Name of the system / master Cognito User Pool"
  type        = string
  default     = "aurora-system-demo"
}

variable "aws_cognito_client_name" {
  description = "Name of the master App Client"
  type        = string
  default     = "aurora-system-client"
}

variable "aws_cognito_domain_prefix" {
  description = "Custom domain prefix for Cognito User Pool (optional)"
  type        = string
  default     = null
}

variable "aws_iam_user_name" {
  description = "IAM user name for IamTenant"
  type        = string
  default     = "aurora-iam-tenant-demo"
}

# 5. External Services — Cloudflare R2
variable "enable_cloudflare_r2" {
  description = "Whether to provision Cloudflare R2 bucket for Mail Service"
  type        = bool
  default     = false
}

variable "cloudflare_account_id" {
  description = "Cloudflare Account ID"
  type        = string
  default     = ""
}

variable "cloudflare_api_token" {
  description = "Cloudflare API Token with R2 permissions (optional if CLOUDFLARE_API_TOKEN env var is set)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "r2_bucket_name" {
  description = "Cloudflare R2 Bucket name for Mail Service"
  type        = string
  default     = "aurora-mail-demo"
}

variable "r2_location_hint" {
  description = "R2 storage location hint (apac, wnam, enam, weur, eeur)"
  type        = string
  default     = "apac"
}
