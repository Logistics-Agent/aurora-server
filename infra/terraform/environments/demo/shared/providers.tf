# Azure Provider
provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

# AWS Provider (For Cognito & IAM)
# Credentials can be passed via tfvars or AWS standard env vars (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
provider "aws" {
  region     = var.aws_region
  access_key = var.aws_access_key != "" ? var.aws_access_key : null
  secret_key = var.aws_secret_key != "" ? var.aws_secret_key : null
}

# Cloudflare Provider (For R2 Storage)
# API token can be passed via tfvars or CLOUDFLARE_API_TOKEN env var
provider "cloudflare" {
  api_token = var.cloudflare_api_token != "" ? var.cloudflare_api_token : null
}
