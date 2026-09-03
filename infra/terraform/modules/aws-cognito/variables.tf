variable "user_pool_name" {
  description = "Name of the system / master Cognito User Pool"
  type        = string
  default     = "aurora-platform-demo"
}

variable "clients" {
  description = "Map of Cognito App Clients to create (e.g. system, admin, staff)"
  type = map(object({
    client_name            = string
    generate_secret        = optional(bool, true)
    access_token_validity  = optional(number, 60)
    id_token_validity      = optional(number, 60)
    refresh_token_validity = optional(number, 30)
    explicit_auth_flows    = optional(list(string))
  }))
  default = {
    "system" = {
      client_name     = "aurora-system-client"
      generate_secret = true
    }
    "admin" = {
      client_name     = "aurora-admin-client"
      generate_secret = true
    }
    "staff" = {
      client_name     = "aurora-staff-client"
      generate_secret = true
    }
  }
}

variable "domain_prefix" {
  description = "Custom domain prefix for Cognito User Pool (optional)"
  type        = string
  default     = null
}

variable "iam_user_name" {
  description = "IAM user name for IamTenant service"
  type        = string
  default     = "aurora-iam-tenant-demo"
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
