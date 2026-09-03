variable "account_id" {
  description = "Cloudflare Account ID"
  type        = string
}

variable "bucket_name" {
  description = "Name of the R2 bucket"
  type        = string
  default     = "aurora-mail-platform"
}

variable "location_hint" {
  description = "R2 storage location hint (apac, wnam, enam, weur, eeur)"
  type        = string
  default     = "apac"
}

