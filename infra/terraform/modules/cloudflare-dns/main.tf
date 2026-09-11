# =============================================================================
# Cloudflare DNS & Domain Management Module
# Manages zone, DNS records for Backend API, Vercel frontends, and Staff Manager
# =============================================================================

# 1. Zone Provisioning or Lookup
resource "cloudflare_zone" "this" {
  count      = var.create_zone ? 1 : 0
  account_id = var.account_id
  zone       = var.zone_name
  plan       = var.zone_plan
  type       = "full"
}

data "cloudflare_zone" "this" {
  count      = var.create_zone ? 0 : (var.zone_id == "" ? 1 : 0)
  name       = var.zone_name
  account_id = var.account_id != "" ? var.account_id : null
}

locals {
  zone_id = var.create_zone ? cloudflare_zone.this[0].id : (var.zone_id != "" ? var.zone_id : data.cloudflare_zone.this[0].id)
}

# -----------------------------------------------------------------------------
# 2. Backend API DNS Record: api.humanak.cyou
# -----------------------------------------------------------------------------
resource "cloudflare_record" "api" {
  zone_id = local.zone_id
  name    = var.api_subdomain
  value   = var.api_target
  type    = var.api_record_type
  proxied = var.api_proxied
  ttl     = var.api_proxied ? 1 : var.ttl
  comment = "Backend API Gateway for Aurora Server (BE)"
}

# -----------------------------------------------------------------------------
# 3. Vercel Subdomain: admin.humanak.cyou
# -----------------------------------------------------------------------------
resource "cloudflare_record" "admin" {
  zone_id = local.zone_id
  name    = var.admin_subdomain
  value   = var.admin_cname_target
  type    = "CNAME"
  proxied = var.admin_proxied
  ttl     = var.admin_proxied ? 1 : var.ttl
  comment = "Admin Portal Frontend (Vercel Host)"
}

# -----------------------------------------------------------------------------
# 4. Vercel Subdomain: system.humanak.cyou
# -----------------------------------------------------------------------------
resource "cloudflare_record" "system" {
  zone_id = local.zone_id
  name    = var.system_subdomain
  value   = var.system_cname_target
  type    = "CNAME"
  proxied = var.system_proxied
  ttl     = var.system_proxied ? 1 : var.ttl
  comment = "System / Super Admin Portal Frontend (Vercel Host)"
}

# -----------------------------------------------------------------------------
# 5. Staff Manager / Main Web App: humanak.cyou (Root / Apex)
# -----------------------------------------------------------------------------
resource "cloudflare_record" "staff_manager_root" {
  zone_id = local.zone_id
  name    = "@"
  value   = var.staff_manager_target
  type    = var.staff_manager_record_type
  proxied = var.staff_manager_proxied
  ttl     = var.staff_manager_proxied ? 1 : var.ttl
  comment = "Staff Manager - Main User Web Application (Apex on Vercel)"
}

# -----------------------------------------------------------------------------
# 6. Optional WWW Subdomain: www.humanak.cyou
# -----------------------------------------------------------------------------
resource "cloudflare_record" "www" {
  count   = var.enable_www_subdomain ? 1 : 0
  zone_id = local.zone_id
  name    = "www"
  value   = var.www_cname_target
  type    = "CNAME"
  proxied = var.www_proxied
  ttl     = var.www_proxied ? 1 : var.ttl
  comment = "WWW CNAME for Main Web Application (Vercel Host)"
}

# -----------------------------------------------------------------------------
# 7. Optional ArgoCD Subdomain: argocd.humanak.cyou
# -----------------------------------------------------------------------------
resource "cloudflare_record" "argocd" {
  count   = var.enable_argocd_subdomain ? 1 : 0
  zone_id = local.zone_id
  name    = var.argocd_subdomain
  value   = var.argocd_target != "" ? var.argocd_target : var.api_target
  type    = var.argocd_record_type
  proxied = var.argocd_proxied
  ttl     = var.argocd_proxied ? 1 : var.ttl
  comment = "ArgoCD Control Plane Web UI (Azure App Gateway)"
}

# -----------------------------------------------------------------------------
# 8. Cloudflare Zone Security & SSL Settings (Optional - Managed on Cloudflare Dashboard)
# -----------------------------------------------------------------------------
# Note: SSL mode and HTTPS redirects can be configured directly in Cloudflare Dashboard
# under SSL/TLS settings to avoid requiring Zone.Settings permissions in API token.
