# =============================================================================
# Subscription 3: Shared Infrastructure — External Services (AWS Cognito & Cloudflare R2)
# =============================================================================

# 1. AWS Cognito User Pool, App Clients (System, Admin, Staff), and IAM for IamTenant
module "aws_cognito" {
  count  = var.enable_aws_cognito ? 1 : 0
  source = "../../../modules/aws-cognito"

  user_pool_name = var.aws_cognito_user_pool_name
  domain_prefix  = var.aws_cognito_domain_prefix
  iam_user_name  = var.aws_iam_user_name
  aws_region     = var.aws_region

  tags = local.tags
}

# 2. Cloudflare R2 Bucket for Mail Service
module "cloudflare_r2" {
  count  = var.enable_cloudflare_r2 && var.cloudflare_account_id != "" ? 1 : 0
  source = "../../../modules/cloudflare-r2"

  account_id    = var.cloudflare_account_id
  bucket_name   = var.r2_bucket_name
  location_hint = var.r2_location_hint
}

# 3. Cloudflare DNS & Domain Management (humanak.cyou)
# Manages api.humanak.cyou (BE), admin & system subdomains (Vercel), humanak.cyou (Staff Manager / Main Web)
module "cloudflare_dns" {
  count  = var.enable_cloudflare_dns ? 1 : 0
  source = "../../../modules/cloudflare-dns"

  account_id  = var.cloudflare_account_id
  zone_name   = var.domain_name
  zone_id     = var.cloudflare_zone_id
  create_zone = var.cloudflare_create_zone
  zone_plan   = var.cloudflare_zone_plan

  # Backend API (api.humanak.cyou)
  api_subdomain   = var.api_subdomain
  api_target      = var.api_target_ip
  api_record_type = var.api_record_type
  api_proxied     = var.api_proxied

  # Vercel Frontends (admin.humanak.cyou, system.humanak.cyou)
  admin_subdomain    = var.admin_subdomain
  admin_cname_target = var.admin_cname_target
  admin_proxied      = var.admin_proxied

  system_subdomain    = var.system_subdomain
  system_cname_target = var.system_cname_target
  system_proxied      = var.system_proxied

  # Staff Manager Main Web App (humanak.cyou - Root/Apex)
  staff_manager_target      = var.staff_manager_target
  staff_manager_record_type = var.staff_manager_record_type
  staff_manager_proxied     = var.staff_manager_proxied

  # WWW Subdomain (www.humanak.cyou)
  enable_www_subdomain = var.enable_www_subdomain
  www_cname_target     = var.www_cname_target
  www_proxied          = var.www_proxied

  # Cloudflare Zone Settings
  configure_zone_settings = var.cloudflare_configure_zone_settings
  ssl_setting             = var.cloudflare_ssl_setting
}

