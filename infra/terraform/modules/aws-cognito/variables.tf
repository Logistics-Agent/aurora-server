variable "user_pool_name" {
  description = "Name of the system / master Cognito User Pool"
  type        = string
  default     = "aurora-system"
}

variable "client_name" {
  description = "Name of the master App Client"
  type        = string
  default     = "aurora-system-client"
}

variable "generate_client_secret" {
  description = "Whether to generate a client secret for the App Client"
  type        = bool
  default     = true
}

variable "domain_prefix" {
  description = "Custom domain prefix for Cognito User Pool (optional)"
  type        = string
  default     = null
}

variable "iam_user_name" {
  description = "IAM user name for IamTenant service"
  type        = string
  default     = "aurora-iam-tenant"
}

variable "aws_region" {
  description = "AWS region for Cognito and IAM resource policies"
  type        = string
  default     = "ap-southeast-1"
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default     = {}
}

