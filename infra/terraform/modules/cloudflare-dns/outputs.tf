# =============================================================================
# Cloudflare DNS Module — Outputs
# =============================================================================

output "zone_id" {
  description = "Cloudflare Zone ID"
  value       = local.zone_id
}

output "zone_name" {
  description = "Managed Zone / Domain name"
  value       = var.zone_name
}

output "name_servers" {
  description = "Cloudflare Name Servers for the domain (Set these at your domain registrar)"
  value       = var.create_zone ? cloudflare_zone.this[0].name_servers : (var.zone_id == "" ? data.cloudflare_zone.this[0].name_servers : null)
}

output "api_fqdn" {
  description = "Fully Qualified Domain Name for Backend API"
  value       = "${var.api_subdomain}.${var.zone_name}"
}

output "admin_fqdn" {
  description = "Fully Qualified Domain Name for Admin Portal (Vercel)"
  value       = "${var.admin_subdomain}.${var.zone_name}"
}

output "system_fqdn" {
  description = "Fully Qualified Domain Name for System Portal (Vercel)"
  value       = "${var.system_subdomain}.${var.zone_name}"
}

output "staff_manager_fqdn" {
  description = "Fully Qualified Domain Name for Staff Manager Main Web"
  value       = var.zone_name
}

output "www_fqdn" {
  description = "Fully Qualified Domain Name for WWW"
  value       = var.enable_www_subdomain ? "www.${var.zone_name}" : null
}
