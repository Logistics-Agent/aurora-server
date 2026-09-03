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
