# =============================================================================
# Cloudflare DNS Module — Variables
# =============================================================================

variable "account_id" {
  description = "Cloudflare Account ID"
  type        = string
  default     = ""
}

variable "zone_name" {
  description = "Root domain name managed on Cloudflare (e.g., humanak.cyou)"
  type        = string
  default     = "humanak.cyou"
}

variable "zone_id" {
  description = "Existing Cloudflare Zone ID (if already created). If empty and create_zone=false, looked up automatically."
  type        = string
  default     = ""
}

variable "create_zone" {
  description = "Whether to create the Cloudflare zone via Terraform (true) or use existing / look up (false)"
  type        = bool
  default     = false
}

variable "zone_plan" {
  description = "Cloudflare plan type (free, pro, business, enterprise)"
  type        = string
  default     = "free"
}

# -----------------------------------------------------------------------------
# 1. Backend API (api.humanak.cyou)
# -----------------------------------------------------------------------------
variable "api_subdomain" {
  description = "Subdomain name for Backend API"
  type        = string
  default     = "api"
}

variable "api_target" {
  description = "Target IP address (A record) or hostname (CNAME record) for Backend API"
  type        = string
}

variable "api_record_type" {
  description = "DNS record type for Backend API (A or CNAME)"
  type        = string
  default     = "A"
}

variable "api_proxied" {
  description = "Whether Cloudflare proxy (orange cloud) is enabled for Backend API"
  type        = bool
  default     = true
}

# -----------------------------------------------------------------------------
# 2. Vercel Subdomain: Admin (admin.humanak.cyou)
# -----------------------------------------------------------------------------
variable "admin_subdomain" {
  description = "Subdomain name for Admin portal on Vercel"
  type        = string
  default     = "admin"
}

variable "admin_cname_target" {
  description = "CNAME target for Admin portal on Vercel"
  type        = string
  default     = "cname.vercel-dns.com"
}

variable "admin_proxied" {
  description = "Whether Cloudflare proxy is enabled for Admin Vercel app (false recommended for Vercel SSL)"
  type        = bool
  default     = false
}

# -----------------------------------------------------------------------------
# 3. Vercel Subdomain: System (system.humanak.cyou)
# -----------------------------------------------------------------------------
variable "system_subdomain" {
  description = "Subdomain name for System / Super Admin portal on Vercel"
  type        = string
  default     = "system"
}

variable "system_cname_target" {
  description = "CNAME target for System portal on Vercel"
  type        = string
  default     = "cname.vercel-dns.com"
}

variable "system_proxied" {
  description = "Whether Cloudflare proxy is enabled for System Vercel app (false recommended for Vercel SSL)"
  type        = bool
  default     = false
}

# -----------------------------------------------------------------------------
# 4. Main Web App / Staff Manager (humanak.cyou - Root / Apex)
# -----------------------------------------------------------------------------
variable "staff_manager_target" {
  description = "Target IP (A record, e.g. 76.76.21.21) or CNAME target (e.g. cname.vercel-dns.com) for Staff Manager main web app"
  type        = string
  default     = "76.76.21.21"
}

variable "staff_manager_record_type" {
  description = "Record type for apex domain (A or CNAME)"
  type        = string
  default     = "A"
}

variable "staff_manager_proxied" {
  description = "Whether Cloudflare proxy is enabled for Staff Manager main web app (false recommended for Vercel SSL)"
  type        = bool
  default     = false
}

# -----------------------------------------------------------------------------
# 5. WWW Subdomain (www.humanak.cyou)
# -----------------------------------------------------------------------------
variable "enable_www_subdomain" {
  description = "Whether to create www subdomain CNAME record"
  type        = bool
  default     = true
}

variable "www_cname_target" {
  description = "CNAME target for WWW subdomain on Vercel"
  type        = string
  default     = "cname.vercel-dns.com"
}

variable "www_proxied" {
  description = "Whether Cloudflare proxy is enabled for www"
  type        = bool
  default     = false
}

# -----------------------------------------------------------------------------
# 6. General DNS & SSL Settings
# -----------------------------------------------------------------------------
variable "ttl" {
  description = "Time to live for DNS records (1 = automatic for proxied records)"
  type        = number
  default     = 1
}

variable "configure_zone_settings" {
  description = "Whether to apply recommended Cloudflare zone settings (SSL, HTTPS redirect, TLS 1.2+)"
  type        = bool
  default     = true
}

variable "ssl_setting" {
  description = "SSL encryption mode for Cloudflare zone (off, flexible, full, strict)"
  type        = string
  default     = "full"
}
